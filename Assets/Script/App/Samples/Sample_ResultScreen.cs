using Seed.Hub.Contracts;
using Seed.UI;
using UnityEngine;

namespace Seed.App
{
    /// <summary>【サンプル】決着後のリザルト画面（表示のみの最小構成）。</summary>
    public sealed class Sample_ResultScreen : UIScreen
    {
        /// <summary>この画面のID。</summary>
        public override ScreenId Id => Sample_ScreenIds.Result;

        /// <summary>表示する結果テキスト（Runnerが決着時に設定する）。</summary>
        private string _resultText = "";

        /// <summary>結果テキストを設定する。</summary>
        public void SetResult(string resultText)
        {
            _resultText = resultText;
        }

        /// <summary>結果を大きく描画する。</summary>
        private void OnGUI()
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 40,
                alignment = TextAnchor.MiddleCenter,
            };
            GUI.Label(new Rect(0f, 0f, Screen.width, Screen.height), _resultText, style);
        }
    }
}
