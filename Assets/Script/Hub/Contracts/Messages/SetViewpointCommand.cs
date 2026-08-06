namespace Seed.Hub.Contracts
{
    /// <summary>
    /// 基本の視点を切り替える命令（FPS ⇄ TPS の切替がこれ）。処理者はカメラ基盤のみ。
    ///
    /// 「基本の視点」はプレイヤーが選ぶ常用カメラで、演出カメラ（Push）に一時的に
    /// 覆われても失われない——演出が終われば自動で戻るのは、切替と重ね合わせを
    /// 別の操作にしているため。
    /// </summary>
    public readonly struct SetViewpointCommand : ICommandMessage
    {
        /// <summary>切り替え先の視点。</summary>
        public readonly ViewpointId Viewpoint;

        /// <summary>ブレンド秒数（0 以下なら即切替＝カット）。負値は既定ブレンドを使う。</summary>
        public readonly float BlendSeconds;

        /// <summary>SetViewpointCommand を生成する。</summary>
        public SetViewpointCommand(ViewpointId viewpoint, float blendSeconds = -1f)
        {
            Viewpoint = viewpoint;
            BlendSeconds = blendSeconds;
        }
    }

    /// <summary>
    /// 視点を一時的に上へ重ねる命令（演出カメラ・照準カメラ）。処理者はカメラ基盤のみ。
    /// 同じ視点を二重に積むことはできない（既に積まれていれば最前面へ移す）。
    /// </summary>
    public readonly struct PushViewpointCommand : ICommandMessage
    {
        /// <summary>重ねる視点。</summary>
        public readonly ViewpointId Viewpoint;

        /// <summary>ブレンド秒数（0 以下なら即切替、負値は既定ブレンド）。</summary>
        public readonly float BlendSeconds;

        /// <summary>PushViewpointCommand を生成する。</summary>
        public PushViewpointCommand(ViewpointId viewpoint, float blendSeconds = -1f)
        {
            Viewpoint = viewpoint;
            BlendSeconds = blendSeconds;
        }
    }

    /// <summary>
    /// 重ねた視点を取り下げる命令（演出の終了）。処理者はカメラ基盤のみ。
    /// 積んだ順に関係なく指定の視点を取り除けるので、演出が入れ違っても破綻しない。
    /// </summary>
    public readonly struct PopViewpointCommand : ICommandMessage
    {
        /// <summary>取り下げる視点（None なら最前面を1枚取り下げる）。</summary>
        public readonly ViewpointId Viewpoint;

        /// <summary>ブレンド秒数（0 以下なら即切替、負値は既定ブレンド）。</summary>
        public readonly float BlendSeconds;

        /// <summary>PopViewpointCommand を生成する。</summary>
        public PopViewpointCommand(ViewpointId viewpoint = default, float blendSeconds = -1f)
        {
            Viewpoint = viewpoint;
            BlendSeconds = blendSeconds;
        }
    }

    /// <summary>
    /// 有効な視点が変わったという通知（過去形）。
    /// HUD の切替（一人称では照準を出す等）や解析ログはこれを購読する。
    /// </summary>
    public readonly struct ViewpointChanged : INotificationMessage
    {
        /// <summary>直前の視点。</summary>
        public readonly ViewpointId Previous;

        /// <summary>現在の視点。</summary>
        public readonly ViewpointId Current;

        /// <summary>ViewpointChanged を生成する。</summary>
        public ViewpointChanged(ViewpointId previous, ViewpointId current)
        {
            Previous = previous;
            Current = current;
        }
    }
}
