using Seed.Data;
using Seed.Flow;
using Seed.Hub;
using Seed.Hub.Contracts;
using Seed.Input;
using UnityEngine;

namespace Seed.App
{
    /// <summary>
    /// 【サンプル】ホームフェーズ（出撃・買い物の起点）。
    /// 「1フェーズ=1合成ルート」の最小例——OnEnter で画面を組み、OnExit で片付ける。
    /// 遷移は ChangePhaseCommand を発行するだけで、行き先の実体は知らない。
    /// </summary>
    public sealed class Sample_HomePhase : GamePhase
    {
        /// <summary>発行先のHub。</summary>
        private readonly MessageHub _hub;

        /// <summary>入力（永続ルートが所有。ここでは読むだけ）。</summary>
        private readonly InputRouter _input;

        /// <summary>ステージ一覧（メニュー表示用）。</summary>
        private readonly MasterDataSet _catalog;

        /// <summary>このフェーズが組んだ表示物のルート。</summary>
        private GameObject _root;

        /// <summary>Sample_HomePhase を生成する。</summary>
        public Sample_HomePhase(MessageHub hub, InputRouter input, MasterDataSet catalog)
        {
            _hub = hub;
            _input = input;
            _catalog = catalog;
        }

        /// <summary>このフェーズのID。</summary>
        public override PhaseId Id => Sample_PhaseIds.Home;

        /// <summary>ホーム画面を組み立てる。</summary>
        public override void OnEnter(int payload)
        {
            var stage1 = _catalog.Get<Sample_StageSpec>(Sample_MasterCatalog.Stage1.Value);
            var stage2 = _catalog.Get<Sample_StageSpec>(Sample_MasterCatalog.Stage2.Value);
            var stage3 = _catalog.Get<Sample_StageSpec>(Sample_MasterCatalog.Stage3.Value);
            var stage4 = _catalog.Get<Sample_StageSpec>(Sample_MasterCatalog.Stage4.Value);
            var stage5 = _catalog.Get<Sample_StageSpec>(Sample_MasterCatalog.Stage5.Value);

            _root = new GameObject("HomePhase");
            var panel = _root.AddComponent<Sample_TextPanel>();
            panel.Title = "ホーム";
            panel.Lines = new[]
            {
                $"[1] 出撃: {stage1.DisplayName}",
                $"[2] 出撃: {stage2.DisplayName}",
                "[3] ショップ",
                $"[4] 出撃: {stage3.DisplayName}",
                $"[5] 出撃: {stage4.DisplayName}",
                $"[6] 出撃: {stage5.DisplayName}",
                "[7] イベント: よろず屋（ADV・3D）",
                "[8] イベント: 幕間の会話（ADV・2D）",
            };
        }

        /// <summary>メニュー入力を遷移命令へ変換する。</summary>
        public override void Tick(float deltaTime)
        {
            if (_input.WasPressedThisFrame(ActionId.Attack)) // [1]
            {
                _hub.PublishCommand(new ChangePhaseCommand(
                    Sample_PhaseIds.Battle, Sample_MasterCatalog.Stage1.Value));
            }
            else if (_input.WasPressedThisFrame(ActionId.Interact)) // [2]
            {
                _hub.PublishCommand(new ChangePhaseCommand(
                    Sample_PhaseIds.Battle, Sample_MasterCatalog.Stage2.Value));
            }
            else if (_input.WasPressedThisFrame(ActionId.Submit)) // [3]
            {
                _hub.PublishCommand(new ChangePhaseCommand(Sample_PhaseIds.Shop));
            }
            else if (_input.WasPressedThisFrame(Sample_ActionIds.Slot4)) // [4]
            {
                _hub.PublishCommand(new ChangePhaseCommand(
                    Sample_PhaseIds.Battle, Sample_MasterCatalog.Stage3.Value));
            }
            else if (_input.WasPressedThisFrame(Sample_ActionIds.Slot5)) // [5]
            {
                _hub.PublishCommand(new ChangePhaseCommand(
                    Sample_PhaseIds.Battle, Sample_MasterCatalog.Stage4.Value));
            }
            else if (_input.WasPressedThisFrame(Sample_ActionIds.Slot6)) // [6]
            {
                _hub.PublishCommand(new ChangePhaseCommand(
                    Sample_PhaseIds.Battle, Sample_MasterCatalog.Stage5.Value));
            }
            else if (_input.WasPressedThisFrame(Sample_ActionIds.Slot7)) // [7]
            {
                _hub.PublishCommand(new ChangePhaseCommand(
                    Sample_PhaseIds.Event, Sample_MasterCatalog.EventShop));
            }
            else if (_input.WasPressedThisFrame(Sample_ActionIds.Slot8)) // [8]
            {
                _hub.PublishCommand(new ChangePhaseCommand(
                    Sample_PhaseIds.Event, Sample_MasterCatalog.EventTalk2D));
            }
        }

        /// <summary>ホーム画面を片付ける。</summary>
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
