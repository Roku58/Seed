namespace Seed.Character
{
    /// <summary>
    /// Behavior が触ってよい世界の全部（Actor が1個所有し、毎Tick詰め直す）。
    /// クラスなのはアロケーションなしで毎フレーム使い回すため。
    /// </summary>
    public sealed class BehaviorContext
    {
        /// <summary>書き込み先の姿勢（移動・旋回はここへ）。</summary>
        public ActorPose Pose;

        /// <summary>意味レベルの演出呼び出し先。</summary>
        public IAvatar Avatar;

        /// <summary>今フレームの意図（Logic 由来）。</summary>
        public CharacterIntent Intent;

        /// <summary>生存しているか（Agent の表示用写し。読み取り専用に使う）。</summary>
        public bool IsAlive;

        /// <summary>移動の解決器（物理・ナビメッシュとの統合点。既定は素通し）。</summary>
        public IMotionSolver MotionSolver;
    }
}
