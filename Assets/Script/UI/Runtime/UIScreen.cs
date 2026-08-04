using Seed.Hub;
using Seed.Hub.Contracts;
using UnityEngine;

namespace Seed.UI
{
    /// <summary>
    /// 画面1枚の基底。
    /// 表示制御はGameObjectのactive切替（スケルトン実装）。
    ///
    /// データへの到達経路の規約:
    /// - 出来事（HP減少など）… OnShow で Hub を購読し、OnHide で解除
    /// - 毎フレームの連続値（位置・ゲージ現在値）… Services から借りた
    ///   問い合わせIF（ICharacterQuery 等）を OnShow 以降の Update で読む
    /// Hub / Services は Router 登録時に Attach で配られるため、
    /// 画面クラスへ具象参照を手渡しする配線は不要になる。
    /// </summary>
    public abstract class UIScreen : MonoBehaviour
    {
        /// <summary>この画面を指す共有ID（値の割り当てはアプリ側の定数クラス）。</summary>
        public abstract ScreenId Id { get; }

        /// <summary>
        /// この画面が属するレイヤ（既定は Base）。
        /// Base=同時1枚 / Overlay=常設の重ね / Modal=入力を奪う前面。
        /// </summary>
        public virtual ScreenLayer Layer => ScreenLayer.Base;

        /// <summary>メッセージ購読の足場（Attach で配られる）。</summary>
        protected MessageHub Hub { get; private set; }

        /// <summary>同期問い合わせの足場（Attach で配られる）。</summary>
        protected ServiceRegistry Services { get; private set; }

        /// <summary>直近の表示で渡された荷物（意味はアプリ定義。対象IDなど）。</summary>
        protected int Payload { get; private set; }

        /// <summary>購読・問い合わせの足場を配る（Router 登録時に UISystem が呼ぶ）。</summary>
        internal void Attach(MessageHub hub, ServiceRegistry services)
        {
            Hub = hub;
            Services = services;
        }

        /// <summary>
        /// 画面を表示状態にし、入り演出を返す（Routerが呼ぶ。null なら即時）。
        /// 状態変更（OnShow・購読開始）は演出を待たず先に完了する。
        /// </summary>
        internal IScreenTransition BeginShow(int payload)
        {
            Payload = payload;
            gameObject.SetActive(true);
            OnShow();
            return CreateShowTransition();
        }

        /// <summary>
        /// 画面を非表示状態にし、抜け演出を返す（Routerが呼ぶ。null なら即時に消える）。
        /// OnHide（購読解除）は即時。演出中も GameObject は表示されたまま残り、
        /// 演出完了時に Router が Deactivate する。
        /// </summary>
        internal IScreenTransition BeginHide()
        {
            OnHide();
            return CreateHideTransition();
        }

        /// <summary>表示物を消す（Router が演出完了時に呼ぶ）。</summary>
        internal void Deactivate()
        {
            gameObject.SetActive(false);
        }

        /// <summary>表示時のフック（購読開始・表示データ取得はここ。荷物は Payload で読める）。</summary>
        protected virtual void OnShow()
        {
        }

        /// <summary>非表示時のフック（購読解除はここ）。</summary>
        protected virtual void OnHide()
        {
        }

        /// <summary>入り演出を作る（既定は null=即時。フェードイン等はここで返す）。</summary>
        protected virtual IScreenTransition CreateShowTransition()
        {
            return null;
        }

        /// <summary>抜け演出を作る（既定は null=即時。フェードアウト等はここで返す）。</summary>
        protected virtual IScreenTransition CreateHideTransition()
        {
            return null;
        }
    }
}
