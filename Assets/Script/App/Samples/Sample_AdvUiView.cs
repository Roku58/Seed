using Seed.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Seed.App
{
    /// <summary>
    /// 【サンプル】ADV画面のガワ（見た目）の結び付け。
    ///
    /// フェーズ（Sample_AdvEventPhase）はこの参照だけを使って表示を組むため、
    /// **本コンポーネントをルートに付けた UI プレハブを用意して各参照を差し、
    /// イベントデータの UiPrefabKey で指定すれば、ロジックはそのまま見た目を丸ごと
    /// 差し替えられる**。プレハブが無ければフェーズが内蔵の既定ガワをコードで組み、
    /// 同じ形でこのコンポーネントへ流し込む（＝内蔵はあくまでフォールバック）。
    ///
    /// [プレハブの契約]
    /// - ルートは Canvas（Screen Space Overlay 推奨）で、本コンポーネントを付ける
    /// - ChoiceTemplate / BubbleTemplate は非アクティブの雛形。必要数だけ複製される
    /// - 重なり順（下から）: TapCatcher → PortraitLayer → BubbleLayer → Window → Choices
    /// </summary>
    public sealed class Sample_AdvUiView : MonoBehaviour
    {
        /// <summary>固定テキストボックス（窓）一式のルート。</summary>
        public GameObject WindowRoot;

        /// <summary>窓の名札（話者名 / ナレーション時はイベント見出し）。</summary>
        public Text PlateText;

        /// <summary>窓の本文（文字送りの流し込み先）。</summary>
        public Text BodyText;

        /// <summary>送り待ちマーク（▼）。</summary>
        public Text PageMark;

        /// <summary>選択肢ボタンを並べる層（レイアウトはプレハブ側の自由）。</summary>
        public RectTransform ChoicesRoot;

        /// <summary>選択肢ボタンの雛形（非アクティブ。選択肢の数だけ複製される）。</summary>
        public SeedButton ChoiceTemplate;

        /// <summary>吹き出しを並べる層。</summary>
        public RectTransform BubbleLayer;

        /// <summary>吹き出しの雛形（非アクティブ。吹き出しの数だけ複製される）。</summary>
        public GameObject BubbleTemplate;

        /// <summary>画面全体の透明な「送り」ボタン。</summary>
        public SeedButton TapCatcher;

        /// <summary>2D舞台（立ち絵）の置き場。3D舞台では使わない。</summary>
        public RectTransform PortraitLayer;

        /// <summary>参照の欠けを検査する（欠けたガワは事故の元＝早期に警告して既定へ落とす）。</summary>
        public bool IsComplete()
        {
            return WindowRoot != null && PlateText != null && BodyText != null
                && PageMark != null && ChoicesRoot != null && ChoiceTemplate != null
                && BubbleLayer != null && BubbleTemplate != null && TapCatcher != null
                && PortraitLayer != null;
        }
    }
}
