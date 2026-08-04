// ============================================================================
// 【サンプルコード】Sample_GameFlowRunner
// ゲーム全体のフロー（ホーム⇄戦闘⇄ショップ）を回す永続ルート。
//
// 空のGameObjectにアタッチしてPlayするだけで動く。
//   ホーム : [1] 草原へ出撃 / [2] 火山へ出撃 / [3] ショップ
//   戦闘   : WASD移動 / [1]攻撃 / [G]ガード / [T]3D⇔2D切替 / [B]ホームへ
//   ショップ: [B]ホームへ
//
// 役割分担（フェーズをまたぐもの vs フェーズの中のもの）:
//   永続ルート（このクラス）… MessageHub / ServiceRegistry / InputRouter /
//                              マスターデータ / GameFlow / カメラ・ライト
//   各フェーズ（GamePhase）  … その場でだけ使う基盤・舞台・画面
//                              （OnEnter で組み、OnExit で逆順に全部消える）
//
// ステージ切り替え＝ ChangePhaseCommand(Battle, StageId.Value) を発行するだけ。
// 同じ戦闘フェーズに別のステージ仕様で再入する（エリア移動も同じ経路）。
// ============================================================================

using Seed.App;
using Seed.Clock;
using Seed.Data;
using Seed.Flow;
using Seed.Hub;
using Seed.Input;
using UnityEngine;

namespace Seed.App
{
    /// <summary>【サンプル】GameFlow を駆動する永続ルート（ゲームの入口）。</summary>
    public sealed class Sample_GameFlowRunner : MonoBehaviour
    {
        /// <summary>仲介基盤: メッセージハブ。</summary>
        private MessageHub _hub;

        /// <summary>仲介基盤: サービス台帳。</summary>
        private ServiceRegistry _services;

        /// <summary>入力基盤: エッジ検出つきルーター（毎フレームここで1回だけ Tick）。</summary>
        private InputRouter _input;

        /// <summary>フロー基盤: フェーズ遷移状態機械。</summary>
        private GameFlow _flow;

        /// <summary>時間基盤: dt の供給源（ポーズ・倍速・ヒットストップ）。</summary>
        private GameClock _clock;

        /// <summary>永続ルート: 生成→フェーズ登録→初期フェーズ予約。</summary>
        private void Start()
        {
            // 1. フェーズをまたいで生きる基盤
            _hub = new MessageHub();
            _services = new ServiceRegistry();
            _input = new InputRouter(new Sample_KeyboardReader());
            _clock = new GameClock();
            _clock.Initialize(_hub, _services);

            // 2. マスターデータ（ユニット＋ステージ。IDは GameCore の値体系と同値運用）
            var catalog = Sample_MasterCatalog.Build(
                new Seed.Hub.Contracts.CharacterId(1), new Seed.Hub.Contracts.CharacterId(2));

            // 3. カメラ・ライト（フェーズをまたいで使い回す）
            BuildCameraAndLight();

            // 4. フローとフェーズ（フェーズの追加＝AddPhase 1行）
            _flow = new GameFlow(_hub);
            _flow.AddPhase(new Sample_HomePhase(_hub, _input, catalog));
            _flow.AddPhase(new Sample_BattlePhase(_hub, _services, _input, catalog));
            _flow.AddPhase(new Sample_ShopPhase(_hub, _input));
            _flow.Start(Sample_PhaseIds.Home);
        }

        /// <summary>毎フレーム: 入力を1回読み、フローに全てを委ねる。</summary>
        private void Update()
        {
            _clock.Tick(Time.deltaTime); // dt の供給源はここだけ（ポーズ・倍速が全基盤へ同時に効く）
            _input.Tick();
            _flow.Tick(_clock.UnscaledDelta); // フェーズ遷移とメニューはポーズ中も動く
        }

        /// <summary>
        /// フレーム末尾: PublishDeferred で積まれた遅延メッセージを配達する。
        /// Update 系の処理（入力・フロー・各フェーズ）が全て終わった後に配達することで、
        /// 「今フレームの連鎖の外へ回す」という PublishDeferred の約束を守る。
        /// </summary>
        private void LateUpdate()
        {
            _hub.Pump();
        }

        /// <summary>滞在中フェーズの片付けまで含めて終了する。</summary>
        private void OnDestroy()
        {
            _flow?.Dispose();
            _clock?.Dispose();
        }

        /// <summary>カメラとライトを用意する（無ければ作る）。</summary>
        private void BuildCameraAndLight()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                camera = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
                camera.tag = "MainCamera";
            }
            camera.transform.position = new Vector3(0f, 8f, -8f);
            camera.transform.LookAt(new Vector3(0f, 1f, 0f));

            if (FindFirstObjectByType<Light>() == null)
            {
                var light = new GameObject("Directional Light", typeof(Light)).GetComponent<Light>();
                light.type = LightType.Directional;
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }
        }
    }
}
