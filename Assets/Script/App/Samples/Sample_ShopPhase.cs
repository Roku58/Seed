using Seed.Flow;
using Seed.Hub;
using Seed.Hub.Contracts;
using Seed.Input;
using UnityEngine;

namespace Seed.App
{
    /// <summary>
    /// 【サンプル】ショップフェーズ（品揃え表示だけの骨組み）。
    /// 実プロジェクトでは品揃えを Seed.Data の定義（ItemSpec 等）から並べ、
    /// 購入は「所持金サービス（永続ルートが所有）」への命令として実装する
    /// ——フェーズは金庫を持たない（フェーズをまたぐ状態は永続ルートの仕事）。
    /// </summary>
    public sealed class Sample_ShopPhase : GamePhase
    {
        /// <summary>発行先のHub。</summary>
        private readonly MessageHub _hub;

        /// <summary>入力（読むだけ）。</summary>
        private readonly InputRouter _input;

        /// <summary>このフェーズが組んだ表示物のルート。</summary>
        private GameObject _root;

        /// <summary>Sample_ShopPhase を生成する。</summary>
        public Sample_ShopPhase(MessageHub hub, InputRouter input)
        {
            _hub = hub;
            _input = input;
        }

        /// <summary>このフェーズのID。</summary>
        public override PhaseId Id => Sample_PhaseIds.Shop;

        /// <summary>ショップ画面を組み立てる。</summary>
        public override void OnEnter(int payload)
        {
            _root = new GameObject("ShopPhase");
            var panel = _root.AddComponent<Sample_TextPanel>();
            panel.Title = "ショップ";
            panel.Lines = new[]
            {
                "回復薬 …… 100G（デモのため陳列のみ）",
                "鬼人薬 …… 300G",
                "",
                "[B] ホームへ戻る",
            };
        }

        /// <summary>戻る入力を遷移命令へ変換する。</summary>
        public override void Tick(float deltaTime)
        {
            if (_input.WasPressedThisFrame(ActionId.Cancel)) // [B]
            {
                _hub.PublishCommand(new ChangePhaseCommand(Sample_PhaseIds.Home));
            }
        }

        /// <summary>ショップ画面を片付ける。</summary>
        public override void OnExit()
        {
            if (_root != null)
            {
                Object.Destroy(_root);
                _root = null;
            }
        }
    }
}
