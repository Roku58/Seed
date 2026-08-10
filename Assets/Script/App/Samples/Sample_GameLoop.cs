using System;
using Seed.Clock;
using Seed.Data;
using Seed.Flow;
using Seed.Hub;
using Seed.Hub.Contracts;
using Seed.Input;
using Seed.Logging;
using UnityEngine;
using VContainer.Unity;

namespace Seed.App
{
    /// <summary>
    /// 【サンプル】ゲーム全体の駆動役（VContainer のエントリポイント）。
    ///
    /// 依存はコンストラクタ注入で受け取る（配線は <see cref="Sample_GameFlowRunner"/> の
    /// Configure に宣言されている）。MonoBehaviour ではないのに毎フレーム動けるのは、
    /// VContainer が IStartable / ITickable / ILateTickable を PlayerLoop へ橋渡しするため。
    ///
    /// [駆動順の責務] dt の加工（Clock）→ 入力（Input）→ フロー（Flow）の順は
    /// ここが一列に並べて保証する。フェーズ内部の順序は TickPipeline（型による順序保証）が
    /// 受け持つ——DI コンテナは「生成と寿命」だけを扱い、駆動順はコードが持つ役割分担。
    /// </summary>
    public sealed class Sample_GameLoop : IStartable, ITickable, ILateTickable, IDisposable
    {
        /// <summary>仲介基盤: メッセージハブ。</summary>
        private readonly MessageHub _hub;

        /// <summary>仲介基盤: サービス台帳。</summary>
        private readonly ServiceRegistry _services;

        /// <summary>ADVシグナル購読の後始末。</summary>
        private IDisposable _advSignal;

        /// <summary>入力基盤（毎フレームここで1回だけ Tick）。</summary>
        private readonly InputRouter _input;

        /// <summary>時間基盤（dt の供給源）。</summary>
        private readonly GameClock _clock;

        /// <summary>フロー基盤（フェーズ遷移状態機械）。</summary>
        private readonly GameFlow _flow;

        /// <summary>登録するフェーズ（追加＝Configure に1行＋ここに1引数）。</summary>
        private readonly Sample_HomePhase _home;

        /// <summary>戦闘フェーズ。</summary>
        private readonly Sample_BattlePhase _battle;

        /// <summary>ショップフェーズ。</summary>
        private readonly Sample_ShopPhase _shop;

        /// <summary>イベントADVフェーズ。</summary>
        private readonly Sample_AdvEventPhase _adv;

        /// <summary>依存を受け取る（生成は VContainer が行う）。</summary>
        public Sample_GameLoop(MessageHub hub, ServiceRegistry services, InputRouter input,
            GameClock clock, GameFlow flow,
            Sample_HomePhase home, Sample_BattlePhase battle, Sample_ShopPhase shop,
            Sample_AdvEventPhase adv)
        {
            _hub = hub;
            _services = services;
            _input = input;
            _clock = clock;
            _flow = flow;
            _home = home;
            _battle = battle;
            _shop = shop;
            _adv = adv;
        }

        /// <summary>起動: 初期化順が意味を持つものをここで一列に並べる。</summary>
        public void Start()
        {
            GameLog.InitializeForUnity(); // ログ基盤（以降の Debug.Log 直呼びを置き換えていく）

            _clock.Initialize(_hub, _services);
            BuildCameraAndLight();

            _flow.AddPhase(_home);
            _flow.AddPhase(_battle);
            _flow.AddPhase(_shop);
            _flow.AddPhase(_adv);
            _flow.Start(Sample_PhaseIds.Home);

            Sample_DebugPage.TryAttach(_hub, _services); // 開発用デバッグメニュー（エディタのみ）

            // ADVの台本からの合図（Signal 演技）を購読する実例——ここに任意の処理を書ける
            // （報酬付与・フラグ更新など。台本は合図を出すだけで、意味づけは購読側の方針）
            _advSignal = _hub.Subscribe<Sample_AdvSignal>(signal =>
                Debug.Log($"[Adv] シグナル受信: {signal.Key}（イベント {signal.EventId}）"));
        }

        /// <summary>毎フレーム: dt の加工が最初、フローはポーズ中も動かす。</summary>
        public void Tick()
        {
            _clock.Tick(Time.deltaTime);
            _input.Tick();
            _flow.Tick(_clock.UnscaledDelta);
        }

        /// <summary>フレーム末尾: 遅延メッセージの配達（今フレームの連鎖の外へ回す約束）。</summary>
        public void LateTick()
        {
            _hub.Pump();
        }

        /// <summary>終了: フローとクロックはコンテナが逆順 Dispose するため、ここではログだけ畳む。</summary>
        public void Dispose()
        {
            _advSignal?.Dispose();
            GameLog.Shutdown();
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

            if (UnityEngine.Object.FindFirstObjectByType<Light>() == null)
            {
                var light = new GameObject("Directional Light", typeof(Light)).GetComponent<Light>();
                light.type = LightType.Directional;
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }
        }
    }
}
