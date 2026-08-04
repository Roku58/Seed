using System;
using System.Collections.Generic;
using Seed.Hub;

namespace Seed.Character
{
    /// <summary>
    /// キャラクターの1表現形態（Presenter）。
    /// Behavior 状態機械・姿勢（ActorPose）・表示物（IAvatar）を束ね、
    /// Logic の意図（CharacterIntent）をもとに Behavior を切り替える。
    ///
    /// 遷移の裁定はここで一元化する（各 Behavior は「提案」しかしない）:
    /// - 現在行動の実効優先度 = IsCompleted ? int.MinValue : Priority
    /// - 次行動の Priority がそれ以上 かつ CanEnter が真なら遷移（Exit→Enter）
    /// - 同一キーへの再入は AllowsRefresh の行動だけ「頭からやり直し」を許す
    /// - force（死亡・Actor切替）はすべて無視して遷移
    ///
    /// 遷移通知（Transitioned→Hub発行→方針→リアクション）の同期連鎖が
    /// 自分自身へ再入するケースは、通知中の要求をキューへ積んで通知後に順次裁定する
    /// ——内側の遷移が先に Avatar へ届いて表示と実状態がズレる事故を構造的に防ぐ。
    ///
    /// Tick の内部順序（決定的・毎回同じ）:
    /// ①意図の反映 → ②遷移解決 → ③現行動の Tick → ④Avatar へ姿勢反映
    /// </summary>
    public sealed class CharacterActor : IAvatarEventSink
    {
        /// <summary>遷移通知中に届いた要求（通知後に順次裁定する）。</summary>
        private readonly struct PendingRequest
        {
            /// <summary>要求された行動。</summary>
            public readonly BehaviorKey Key;

            /// <summary>行動に添える荷物。</summary>
            public readonly int Payload;

            /// <summary>強制遷移か。</summary>
            public readonly bool Force;

            /// <summary>PendingRequest を生成する。</summary>
            public PendingRequest(BehaviorKey key, int payload, bool force)
            {
                Key = key;
                Payload = payload;
                Force = force;
            }
        }

        /// <summary>登録済みの行動。</summary>
        private readonly Dictionary<BehaviorKey, ICharacterBehavior> _behaviors =
            new Dictionary<BehaviorKey, ICharacterBehavior>();

        /// <summary>Behavior に見せる文脈（使い回してアロケーションを避ける）。</summary>
        private readonly BehaviorContext _context;

        /// <summary>遷移通知中に届いた要求の待機列。</summary>
        private readonly List<PendingRequest> _pendingRequests = new List<PendingRequest>(2);

        /// <summary>現在の行動（Activate 前と Deactivate 後は null）。</summary>
        private ICharacterBehavior _current;

        /// <summary>遷移（Exit→Enter→通知）の最中か。</summary>
        private bool _isTransitioning;

        /// <summary>CharacterActor を生成する。</summary>
        public CharacterActor(ActorKey key, IAvatar avatar)
        {
            if (avatar == null)
            {
                throw new ArgumentNullException(nameof(avatar), "表示なしなら NullAvatar.Instance を明示的に渡す");
            }
            Key = key;
            Avatar = avatar;
            Pose = new ActorPose();
            _context = new BehaviorContext
            {
                Pose = Pose,
                Avatar = avatar,
                Intent = CharacterIntent.None,
                IsAlive = true,
                MotionSolver = DirectMotionSolver.Instance,
            };
        }

        /// <summary>ユニット内でこの表現形態を指すキー。</summary>
        public ActorKey Key { get; }

        /// <summary>姿勢（表示用の写し）。</summary>
        public ActorPose Pose { get; }

        /// <summary>表示物。</summary>
        public IAvatar Avatar { get; }

        /// <summary>
        /// 移動の解決器（物理・ナビメッシュとの統合点）。
        /// 既定は素通し（DirectMotionSolver）。CharacterController 等で衝突させたい
        /// Actor には合成ルートが差し替える。null 代入は素通しに戻る。
        /// </summary>
        public IMotionSolver MotionSolver
        {
            get => _context.MotionSolver;
            set => _context.MotionSolver = value ?? DirectMotionSolver.Instance;
        }

        /// <summary>現在の行動キー（未起動なら None）。</summary>
        public BehaviorKey CurrentKey => _current != null ? _current.Key : BehaviorKey.None;

        /// <summary>現在の行動（デバッグ・拡張用。未起動なら null）。</summary>
        public ICharacterBehavior CurrentBehavior => _current;

        /// <summary>表示に立っているか。</summary>
        public bool IsActive { get; private set; }

        /// <summary>
        /// 行動が遷移した（from, to, payload）。
        /// Agent が購読して CharacterBehaviorEvent へ変換する観測点。
        /// </summary>
        public event Action<BehaviorKey, BehaviorKey, int> Transitioned;

        /// <summary>行動を登録する。同一キーの二重登録は構成ミスとして例外。</summary>
        public void AddBehavior(ICharacterBehavior behavior)
        {
            if (behavior == null)
            {
                throw new ArgumentNullException(nameof(behavior));
            }
            if (_behaviors.ContainsKey(behavior.Key))
            {
                throw new HubException($"{behavior.Key} は登録済み（行動キーの重複は構成ミス）");
            }
            _behaviors.Add(behavior.Key, behavior);
        }

        /// <summary>
        /// 行動遷移を要求する。裁定規則に通れば Exit→Enter して true。
        /// 遷移通知の最中に呼ばれた場合はキューへ積み、通知後に順次裁定する（戻り値は true）。
        /// 未登録キーは打ち間違い・登録漏れとして例外。
        /// </summary>
        public bool RequestBehavior(BehaviorKey key, int payload = 0, bool force = false)
        {
            if (!_behaviors.TryGetValue(key, out var next))
            {
                throw new HubException($"{key} は未登録の行動（AddBehavior 漏れかキーの打ち間違い）");
            }

            // 遷移通知の同期連鎖からの再入は、順序を守るためキューへ積んで後で裁定する
            if (_isTransitioning)
            {
                _pendingRequests.Add(new PendingRequest(key, payload, force));
                return true;
            }

            if (!TryTransition(next, key, payload, force))
            {
                return false;
            }
            FlushPendingRequests();
            return true;
        }

        /// <summary>今フレームの意図を差し込む（Tick の前に呼ぶ）。</summary>
        public void SetIntent(in CharacterIntent intent)
        {
            _context.Intent = intent;
        }

        /// <summary>生存写しを同期する（通常は Agent が毎Tick自動で呼ぶ）。</summary>
        public void SetAlive(bool isAlive)
        {
            _context.IsAlive = isAlive;
        }

        /// <summary>1Tick進める。②遷移解決 → ③行動Tick → ④姿勢反映。</summary>
        public void Tick(float deltaTime)
        {
            if (_current == null)
            {
                throw new HubException($"{Key} は未起動（Activate 前に Tick が呼ばれた。配線順を確認）");
            }

            ResolveTransition();
            _current.Tick(_context, deltaTime);
            Avatar.ApplyPose(Pose.Position, Pose.Rotation);
        }

        /// <summary>表示に立てる（Actor 切替時に Agent が呼ぶ）。</summary>
        public void Activate(BehaviorKey initialBehavior)
        {
            IsActive = true;
            Avatar.SetActive(true);
            Avatar.BindEventSink(this);
            RequestBehavior(initialBehavior, 0, force: true);
            Avatar.ApplyPose(Pose.Position, Pose.Rotation); // 1フレームの表示ズレを作らない
        }

        /// <summary>表示から降ろす（現行動を Exit し、以後 Tick されない）。</summary>
        public void Deactivate()
        {
            if (_current != null)
            {
                _current.Exit(_context);
                _current = null;
            }
            IsActive = false;
            Avatar.BindEventSink(null);
            Avatar.SetActive(false);
        }

        /// <summary>
        /// アニメーションイベントを現在の行動へ届ける（IAvatarEventSink 実装）。
        /// 「演出の時間軸」を「論理の時間軸」へ戻す唯一の逆流経路。
        /// </summary>
        public void PostAvatarEvent(int eventId)
        {
            _current?.OnAvatarEvent(_context, eventId);
        }

        /// <summary>
        /// 全行動の内部状態を初期化する（プールからの再利用時に Factory が呼ぶ）。
        /// 表示から降りている状態で呼ぶこと。
        /// </summary>
        public void ResetBehaviors()
        {
            foreach (var behavior in _behaviors.Values)
            {
                behavior.Reset();
            }
            _pendingRequests.Clear();
            _context.Intent = CharacterIntent.None;
            _context.IsAlive = true;
        }

        /// <summary>遷移を1件裁定・実行する（通知はフラグで再入を検知する）。</summary>
        private bool TryTransition(ICharacterBehavior next, BehaviorKey key, int payload, bool force)
        {
            // 同一行動への再入は AllowsRefresh の行動だけ「頭からやり直し」を許す
            if (_current != null && _current.Key.Equals(key) && !next.AllowsRefresh)
            {
                return false;
            }

            if (!force)
            {
                var effective = _current == null || _current.IsCompleted
                    ? int.MinValue
                    : _current.Priority;
                if (next.Priority < effective)
                {
                    return false;
                }
                if (!next.CanEnter(_context))
                {
                    return false;
                }
            }

            var previous = CurrentKey;
            _isTransitioning = true;
            try
            {
                _current?.Exit(_context);
                _current = next;
                next.Enter(_context);
                Transitioned?.Invoke(previous, key, payload);
                Avatar.OnBehaviorChanged(previous, key);
            }
            finally
            {
                _isTransitioning = false;
            }
            return true;
        }

        /// <summary>遷移通知中に積まれた要求を、届いた順に裁定する。</summary>
        private void FlushPendingRequests()
        {
            // 裁定中にさらに積まれても、このループが届いた順に処理する
            for (var i = 0; i < _pendingRequests.Count; i++)
            {
                var request = _pendingRequests[i];
                if (_behaviors.TryGetValue(request.Key, out var next))
                {
                    TryTransition(next, request.Key, request.Payload, request.Force);
                }
            }
            _pendingRequests.Clear();
        }

        /// <summary>現行動の提案をもとに遷移を解決する。</summary>
        private void ResolveTransition()
        {
            var desired = _current.DesiredTransition(_context);
            if (desired.Equals(BehaviorKey.None))
            {
                // 完了した行動を放置しない保険（標準行動は自分で Idle を提案するが、
                // アプリ独自行動が提案を忘れても待機へ戻れるように）
                if (!_current.IsCompleted || _current.Key.Equals(BehaviorKey.Idle))
                {
                    return;
                }
                desired = BehaviorKey.Idle;
            }
            if (desired.Equals(_current.Key))
            {
                return;
            }

            // 意図由来の行動要求なら荷物（技IDなど）を遷移イベントへ乗せる
            var payload = desired.Equals(_context.Intent.RequestedAction)
                ? _context.Intent.ActionPayload
                : 0;
            RequestBehavior(desired, payload);
        }
    }
}
