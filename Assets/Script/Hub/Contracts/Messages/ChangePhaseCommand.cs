namespace Seed.Hub.Contracts
{
    /// <summary>
    /// ゲームフェーズの切り替えを依頼する命令（処理者はフロー基盤のみ）。
    /// どの基盤・画面からでも発行できるが、フェーズの実体と遷移の作法はフロー基盤しか知らない。
    ///
    /// ステージ切り替えは「同じフェーズへ別の荷物で再入」する形で表現する
    /// （例: ChangePhaseCommand(Battle, stageId)。エリア移動・ステージ選択とも同じ経路）。
    /// </summary>
    public readonly struct ChangePhaseCommand : ICommandMessage
    {
        /// <summary>切り替え先のフェーズ。</summary>
        public readonly PhaseId Phase;

        /// <summary>フェーズへ渡す荷物（StageId の値など。意味はアプリ定義）。</summary>
        public readonly int Payload;

        /// <summary>ChangePhaseCommand を生成する。</summary>
        public ChangePhaseCommand(PhaseId phase, int payload = 0)
        {
            Phase = phase;
            Payload = payload;
        }
    }

    /// <summary>
    /// フェーズが切り替わったという出来事（通知・過去形）。
    /// 新フェーズの OnEnter 完了後に発行される。BGM切替・解析ログ等が購読する。
    /// </summary>
    public readonly struct PhaseChanged : INotificationMessage
    {
        /// <summary>直前のフェーズ（初回起動は None）。</summary>
        public readonly PhaseId Previous;

        /// <summary>現在のフェーズ。</summary>
        public readonly PhaseId Current;

        /// <summary>遷移に添えられた荷物。</summary>
        public readonly int Payload;

        /// <summary>PhaseChanged を生成する。</summary>
        public PhaseChanged(PhaseId previous, PhaseId current, int payload)
        {
            Previous = previous;
            Current = current;
            Payload = payload;
        }
    }
}
