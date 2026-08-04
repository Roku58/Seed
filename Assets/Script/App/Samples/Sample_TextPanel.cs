using UnityEngine;

namespace Seed.App
{
    /// <summary>
    /// 【サンプル】タイトルと数行のテキストを出すだけの簡易パネル（OnGUI）。
    /// ホーム・ショップなど「画面1枚で足りるフェーズ」の最小表示に使う。
    /// 実プロジェクトでは uGUI/UI Toolkit の画面（UIScreen）に置き換える。
    /// </summary>
    public sealed class Sample_TextPanel : MonoBehaviour
    {
        /// <summary>見出し。</summary>
        public string Title = "";

        /// <summary>本文（1要素=1行）。</summary>
        public string[] Lines = System.Array.Empty<string>();

        /// <summary>テキストを描画する。</summary>
        private void OnGUI()
        {
            GUI.Label(new Rect(16f, 8f, 900f, 30f), $"== {Title} ==");
            for (var i = 0; i < Lines.Length; i++)
            {
                GUI.Label(new Rect(16f, 40f + i * 22f, 900f, 22f), Lines[i]);
            }
        }
    }
}
