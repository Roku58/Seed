namespace Seed.Hub.Contracts
{
    /// <summary>
    /// ゲーム時間の「今」を問い合わせる窓口（ServiceRegistry 経由で借りる）。
    ///
    /// dt の供給源を一本化する時間基盤の読み取り面。
    /// - ScaledDelta   … ゲーム進行用のdt（ポーズ中・ヒットストップ中は 0、倍速/スローで伸縮）
    /// - UnscaledDelta … 実時間のdt（ポーズメニューのUIアニメ等、止まってはいけないもの用）
    /// 操作（ポーズ・倍速・ヒットストップ）は命令（SetPausedCommand 等）で行い、
    /// 処理者は時間基盤（Seed.Clock）のみ。
    /// </summary>
    public interface IGameClock
    {
        /// <summary>時間倍率（1=等速。0.1でスローモーション、2で倍速）。</summary>
        float Scale { get; }

        /// <summary>ポーズ中か。</summary>
        bool IsPaused { get; }

        /// <summary>今フレームのゲーム進行dt（秒。ポーズ/ヒットストップ中は0）。</summary>
        float ScaledDelta { get; }

        /// <summary>今フレームの実時間dt（秒）。</summary>
        float UnscaledDelta { get; }
    }

    /// <summary>ポーズ状態の切り替えを依頼する命令（処理者は時間基盤のみ）。</summary>
    public readonly struct SetPausedCommand : ICommandMessage
    {
        /// <summary>ポーズするか。</summary>
        public readonly bool IsPaused;

        /// <summary>SetPausedCommand を生成する。</summary>
        public SetPausedCommand(bool isPaused)
        {
            IsPaused = isPaused;
        }
    }

    /// <summary>時間倍率の変更を依頼する命令（処理者は時間基盤のみ）。</summary>
    public readonly struct SetTimeScaleCommand : ICommandMessage
    {
        /// <summary>新しい倍率（0以上。0はポーズと同義の停止）。</summary>
        public readonly float Scale;

        /// <summary>SetTimeScaleCommand を生成する。</summary>
        public SetTimeScaleCommand(float scale)
        {
            Scale = scale;
        }
    }

    /// <summary>
    /// ヒットストップ（打撃の瞬間だけゲーム時間を止める演出）を依頼する命令。
    /// 要求が重なった場合は長い方が残る（加算はしない）。処理者は時間基盤のみ。
    /// </summary>
    public readonly struct HitStopCommand : ICommandMessage
    {
        /// <summary>停止する実時間（秒）。</summary>
        public readonly float Seconds;

        /// <summary>HitStopCommand を生成する。</summary>
        public HitStopCommand(float seconds)
        {
            Seconds = seconds;
        }
    }
}
