namespace Seed.Flow
{
    /// <summary>
    /// ゲームフェーズ1つ（ホーム・戦闘・ショップ…）の基底。
    ///
    /// 規約「1フェーズ=1合成ルート」——OnEnter で必要な基盤・シーン・画面を組み立て
    /// （CompositionScope に預け）、OnExit で逆順に片付ける。
    /// フェーズをまたいで生き残るもの（Hub・ServiceRegistry・入力・フロー自身）は
    /// アプリの永続ルートが持ち、コンストラクタで注入する。
    ///
    /// ステージ切り替えは「同じフェーズへ別の payload で再入」で表現する
    /// （GameFlow は同一フェーズへの遷移でも OnExit→OnEnter を完全に回す）。
    /// フェーズをまたいで持ち越したい状態（編成・所持金など）は
    /// フェーズに置かず、永続ルートが持つサービスに置くこと。
    /// </summary>
    public abstract class GamePhase
    {
        /// <summary>このフェーズを指すID（値の割り当てはアプリ側の定数クラス）。</summary>
        public abstract Seed.Hub.Contracts.PhaseId Id { get; }

        /// <summary>
        /// 入る前の非同期作業（シーンロード等）を返す。null なら即時に入る。
        /// 前のフェーズの OnExit 後に呼ばれ、IsDone まで GameFlow が待つ。
        /// </summary>
        public virtual IFlowOperation CreateLoadOperation(int payload)
        {
            return null;
        }

        /// <summary>フェーズに入る（合成ルートの組み立て。payload はステージID等）。</summary>
        public virtual void OnEnter(int payload)
        {
        }

        /// <summary>フェーズから出る（組み立ての逆順で片付ける）。</summary>
        public virtual void OnExit()
        {
        }

        /// <summary>フェーズ滞在中の毎フレーム駆動（遷移中は呼ばれない）。</summary>
        public virtual void Tick(float deltaTime)
        {
        }
    }
}
