using System.Collections.Generic;
using Seed.Hub;
using Seed.Hub.Contracts;

namespace Seed.Flow
{
    /// <summary>
    /// ゲームフェーズの遷移状態機械（フロー基盤の本体・純C#）。
    ///
    /// - ChangePhaseCommand の唯一の処理者（SubscribeCommand で強制）
    /// - 遷移は「要求 → 次Tickで OnExit → 非同期ロード待ち → OnEnter → PhaseChanged 発行」
    ///   の一本道。フェーズ自身の Tick 中に要求が届いても、実行中のフェーズを
    ///   スタック上で破棄しない（遅延実行で構造的に安全）
    /// - 同一フェーズへの再入も完全に Exit→Enter を回す（＝ステージ切り替えの標準経路）
    /// - 遷移中（ロード待ち）に届いた要求は最新1件だけ保持し、入場完了後に処理する
    ///
    /// 使い方: 合成ルートが AddPhase → Start → 毎フレーム Tick(dt)。
    /// フェーズ間で生き残る状態はフェーズではなく永続ルート側のサービスに置くこと。
    /// </summary>
    public sealed class GameFlow : System.IDisposable
    {
        /// <summary>保留中の遷移要求。</summary>
        private readonly struct PendingChange
        {
            /// <summary>行き先。</summary>
            public readonly PhaseId Phase;

            /// <summary>荷物。</summary>
            public readonly int Payload;

            /// <summary>PendingChange を生成する。</summary>
            public PendingChange(PhaseId phase, int payload)
            {
                Phase = phase;
                Payload = payload;
            }
        }

        /// <summary>登録済みフェーズ。</summary>
        private readonly Dictionary<PhaseId, GamePhase> _phases = new Dictionary<PhaseId, GamePhase>();

        /// <summary>発行先のHub。</summary>
        private readonly MessageHub _hub;

        /// <summary>保持中の購読。</summary>
        private readonly SubscriptionBag _subscriptions = new SubscriptionBag(1);

        /// <summary>滞在中のフェーズ（遷移中は null）。</summary>
        private GamePhase _current;

        /// <summary>直前のフェーズID（PhaseChanged 用）。</summary>
        private PhaseId _previousId = PhaseId.None;

        /// <summary>保留中の遷移要求（最新1件・latest wins）。</summary>
        private PendingChange? _pending;

        /// <summary>入場待ちのフェーズ（ロード中）。</summary>
        private GamePhase _entering;

        /// <summary>入場待ちの荷物。</summary>
        private int _enteringPayload;

        /// <summary>進行中のロード作業。</summary>
        private IFlowOperation _loading;

        /// <summary>GameFlow を生成し、ChangePhaseCommand の処理者になる。</summary>
        public GameFlow(MessageHub hub)
        {
            _hub = hub ?? throw new System.ArgumentNullException(nameof(hub));
            hub.SubscribeCommand<ChangePhaseCommand>(OnChangePhase).AddTo(_subscriptions);
        }

        /// <summary>滞在中のフェーズID（遷移中は None）。</summary>
        public PhaseId Current => _current != null ? _current.Id : PhaseId.None;

        /// <summary>遷移（ロード待ち含む）の最中か。ローディング表示の判定に使える。</summary>
        public bool IsTransitioning => _entering != null || _pending.HasValue;

        /// <summary>進行中ロードの進捗（遷移中でなければ 1）。</summary>
        public float LoadProgress => _loading != null ? _loading.Progress : 1f;

        /// <summary>フェーズを登録する。同一IDの二重登録は構成ミスとして例外。</summary>
        public void AddPhase(GamePhase phase)
        {
            if (phase == null)
            {
                throw new System.ArgumentNullException(nameof(phase));
            }
            if (_phases.ContainsKey(phase.Id))
            {
                throw new HubException($"{phase.Id} は登録済み（フェーズIDの重複は構成ミス）");
            }
            _phases.Add(phase.Id, phase);
        }

        /// <summary>
        /// 最初のフェーズを予約する（実際の入場は次の Tick。ロードがあればその完了後）。
        /// </summary>
        public void Start(PhaseId initial, int payload = 0)
        {
            RequestChange(initial, payload);
        }

        /// <summary>
        /// フェーズ遷移を要求する（未登録IDは打ち間違いとして例外）。
        /// 実行は次の Tick ——フェーズ自身の Tick 中から呼んでも安全。
        /// 遷移中の再要求は最新1件が勝つ。
        /// </summary>
        public void RequestChange(PhaseId phase, int payload = 0)
        {
            if (!_phases.ContainsKey(phase))
            {
                throw new HubException($"{phase} は未登録のフェーズ（AddPhase 漏れかIDの打ち間違い）");
            }
            _pending = new PendingChange(phase, payload);
        }

        /// <summary>
        /// 1フレーム進める。遷移の解決 → ロード待ち → 入場 → 滞在フェーズの Tick の順。
        /// </summary>
        public void Tick(float deltaTime)
        {
            // 1. ロード待ちの完了確認 → 入場
            if (_entering != null)
            {
                if (_loading != null && !_loading.IsDone)
                {
                    return; // ロード中はどのフェーズも Tick しない（ローディング表示は LoadProgress で）
                }
                var entering = _entering;
                var payload = _enteringPayload;
                _entering = null;
                _loading = null;
                _current = entering;
                entering.OnEnter(payload);
                _hub.Publish(new PhaseChanged(_previousId, entering.Id, payload));
            }

            // 2. 保留中の遷移要求を開始（退場 → ロード開始）
            if (_pending.HasValue && _entering == null)
            {
                var pending = _pending.Value;
                _pending = null;
                BeginTransition(pending.Phase, pending.Payload);
                return; // 退場したフレームでは Tick しない（次Tick以降で入場）
            }

            // 3. 滞在フェーズの駆動
            _current?.Tick(deltaTime);
        }

        /// <summary>滞在中のフェーズを退場させて片付ける（アプリ終了・シーン破棄時）。</summary>
        public void Dispose()
        {
            _subscriptions.Dispose();
            _current?.OnExit();
            _current = null;
            _entering = null;
            _loading = null;
            _pending = null;
        }

        /// <summary>命令を遷移要求へ流す。</summary>
        private void OnChangePhase(ChangePhaseCommand command)
        {
            RequestChange(command.Phase, command.Payload);
        }

        /// <summary>退場とロード開始（入場は Tick が完了を見てから行う）。</summary>
        private void BeginTransition(PhaseId phase, int payload)
        {
            if (_current != null)
            {
                _previousId = _current.Id;
                _current.OnExit();
                _current = null;
            }
            var next = _phases[phase];
            _entering = next;
            _enteringPayload = payload;
            _loading = next.CreateLoadOperation(payload); // null = 即時（次Tickで入場）
        }
    }
}
