namespace Seed.Hub.Contracts
{
    /// <summary>
    /// 通知メッセージの印（過去形の出来事）。
    /// 購読者は0人でも成立し、複数基盤が購読してよい。
    /// </summary>
    public interface INotificationMessage
    {
    }

    /// <summary>
    /// 命令メッセージの印（〜Command。「してほしい」）。
    ///
    /// 「命令の処理者は1基盤」という規約を型とランタイムで強制するための印。
    /// この印が付いた型は MessageHub.SubscribeCommand / PublishCommand を使う
    /// （二重購読・処理者不在がその場で例外になる）。
    /// 通常の Subscribe/Publish に命令型を渡すと例外になるため、規約違反が実行時に必ず露見する。
    /// </summary>
    public interface ICommandMessage
    {
    }
}
