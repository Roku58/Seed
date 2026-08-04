using System;
using System.Collections.Generic;
using Seed.Hub;
using Seed.Hub.Contracts;

namespace Seed.Character
{
    /// <summary>
    /// キャラクター1ユニット。
    /// 複数の Actor（表現形態: 3Dモデル・2D立ち絵など）を束ね、状況に応じて切り替える。
    /// Hub 由来のリアクション命令（被弾・死亡）の受け口と、生存の表示用写しの持ち主でもある。
    ///
    /// ゲームルール（HP・ダメージ）は持たない——生死すら「表示用の写し」で、
    /// 真実はロジック側（GameCore）にあり、通知を受けて更新されるだけ。
    /// </summary>
    public sealed class CharacterAgent
    {
        /// <summary>登録済みの Actor。</summary>
        private readonly Dictionary<ActorKey, CharacterActor> _actors =
            new Dictionary<ActorKey, CharacterActor>();

        /// <summary>表示中の Actor。</summary>
        private CharacterActor _active;

        /// <summary>ActiveActor の Transitioned に張った転送ハンドラ（切替時に付け替える）。</summary>
        private readonly Action<BehaviorKey, BehaviorKey, int> _onTransitioned;

        /// <summary>CharacterAgent を生成する。</summary>
        public CharacterAgent(CharacterId id)
        {
            Id = id;
            _onTransitioned = (from, to, payload) =>
                BehaviorStarted?.Invoke(new CharacterBehaviorEvent(Id, to, payload));
        }

        /// <summary>境界を越えて自分を指すID。</summary>
        public CharacterId Id { get; }

        /// <summary>生存表示（表示用の写し。真実はロジック側）。</summary>
        public bool IsAlive { get; private set; } = true;

        /// <summary>表示中の Actor（未登録なら null）。</summary>
        public CharacterActor ActiveActor => _active;

        /// <summary>
        /// 表示中の Actor で行動が始まった（Id 付きの再発火）。
        /// 「Attack 開始 → AttackRequested 発行」のような Hub への変換はアプリの方針。
        /// </summary>
        public event Action<CharacterBehaviorEvent> BehaviorStarted;

        /// <summary>Actor を登録する。同一キーは構成ミスとして例外。activate で即座に表示に立てる。</summary>
        public void AddActor(CharacterActor actor, bool activate = false)
        {
            if (actor == null)
            {
                throw new ArgumentNullException(nameof(actor));
            }
            if (_actors.ContainsKey(actor.Key))
            {
                throw new HubException($"{actor.Key} は登録済み（Actorキーの重複は構成ミス）");
            }
            _actors.Add(actor.Key, actor);

            if (activate)
            {
                if (_active != null)
                {
                    DetachActive();
                }
                AttachActive(actor);
            }
            else
            {
                // 「表示中の Actor は常に1体」の不変条件を保つため、控えは隠しておく
                actor.Avatar.SetActive(false);
            }
        }

        /// <summary>
        /// 表示 Actor を切り替える。姿勢（位置・向き）は引き継ぎ、行動の途中経過は引き継がない
        /// ——切替は「表現形態の交代」であり「行動の続行」ではない。
        /// 未登録キー・同一キーは false。
        /// </summary>
        public bool SwitchActor(ActorKey key)
        {
            if (!_actors.TryGetValue(key, out var next))
            {
                return false;
            }
            if (_active == next)
            {
                return false;
            }

            if (_active != null)
            {
                next.Pose.CopyFrom(_active.Pose);
                DetachActive();
            }
            AttachActive(next);
            return true;
        }

        /// <summary>今フレームの意図を表示中の Actor へ流す。</summary>
        public void SetIntent(in CharacterIntent intent)
        {
            RequireActive().SetIntent(in intent);
        }

        /// <summary>表示中の Actor だけを1Tick進める（非表示の Actor は凍結）。</summary>
        public void Tick(float deltaTime)
        {
            var active = RequireActive();
            active.SetAlive(IsAlive);
            active.Tick(deltaTime);
        }

        /// <summary>
        /// リアクション命令の注入口（即時適用）。
        /// 陣営 Tick 中の割り込みで順序が乱れないよう、Hub 経由の命令は
        /// CharactersManager が「Tick 中は遅延・Tick 後に一括適用」の采配をしてから
        /// ここへ届ける（直接呼ぶのはテスト・演出スクリプトなど順序を自分で管理できる場合のみ）。
        /// </summary>
        public void PostReaction(ReactionId reaction, int payload = 0)
        {
            ReactionTranslator.Apply(this, RequireActive(), reaction, payload);
        }

        /// <summary>
        /// 全 Actor の表示物を解放する（ユニット退場時に UnitManager から一気通貫で呼ばれる）。
        /// 破棄の契約を基盤が持つことで、デスポーン時の GameObject リークを構造的に防ぐ。
        /// </summary>
        public void ReleaseAll()
        {
            if (_active != null)
            {
                DetachActive();
            }
            foreach (var actor in _actors.Values)
            {
                actor.Avatar.Release();
            }
            _actors.Clear();
        }

        /// <summary>生存写しを落とす（ReactionTranslator の死亡処理から呼ばれる）。</summary>
        internal void MarkDead()
        {
            IsAlive = false;
        }

        /// <summary>
        /// プールから再利用する前の初期化。生存写しを立て直し、全 Actor の行動状態を
        /// リセットして、表示中の Actor を待機から再出発させる。
        /// 位置は呼び出し側がスポーン地点を入れ直す（Pose.Position への代入）。
        /// </summary>
        public void ResetForReuse()
        {
            IsAlive = true;
            foreach (var actor in _actors.Values)
            {
                actor.ResetBehaviors();
                actor.SetAlive(true);
                actor.Pose.PlanarSpeed = 0f;
            }
            if (_active != null)
            {
                _active.RequestBehavior(BehaviorKey.Idle, 0, force: true);
            }
        }

        /// <summary>表示中の Actor を返す。未登録は配線漏れとして例外。</summary>
        private CharacterActor RequireActive()
        {
            if (_active == null)
            {
                throw new HubException($"{Id} に表示中の Actor がいない（AddActor(activate: true) の漏れ）");
            }
            return _active;
        }

        /// <summary>Actor を表示に立て、遷移イベントの転送を張る。</summary>
        private void AttachActive(CharacterActor actor)
        {
            _active = actor;
            actor.Transitioned += _onTransitioned;
            actor.Activate(IsAlive ? BehaviorKey.Idle : BehaviorKey.Death);
        }

        /// <summary>Actor を表示から降ろし、遷移イベントの転送を外す。</summary>
        private void DetachActive()
        {
            _active.Transitioned -= _onTransitioned;
            _active.Deactivate();
            _active = null;
        }
    }
}
