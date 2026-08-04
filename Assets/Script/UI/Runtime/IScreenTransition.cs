namespace Seed.UI
{
    /// <summary>
    /// 画面の出入り演出（フェード・スライド等）の契約。
    ///
    /// 画面が CreateShowTransition / CreateHideTransition で返し、Router が毎フレーム
    /// Tick で進める。null を返せば即時切替（既定）——演出は後付けの差し込み口であり、
    /// 状態変更（OnShow/OnHide・購読の開始/解除）は演出の完了を待たず先に行われる。
    /// つまり演出はあくまで「艶」で、論理状態の真実には関与しない
    /// （入力を止めたい場合はアプリが Router.IsTransitionRunning を見て判断する）。
    /// </summary>
    public interface IScreenTransition
    {
        /// <summary>完了したか。</summary>
        bool IsDone { get; }

        /// <summary>演出を1フレーム進める（実時間dtを渡す＝ポーズ中も画面演出は動ける）。</summary>
        void Tick(float deltaTime);

        /// <summary>
        /// 即座に終端状態へ飛ばす（スキップ・割り込み時に Router が呼ぶ）。
        /// 呼ばれた後は IsDone が true を返すこと。
        /// </summary>
        void Complete();
    }
}
