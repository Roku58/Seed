// ============================================================================
// 【サンプルコード】Sample_BattlePhase
// 戦闘フェーズ＝「1フェーズ=1合成ルート」の本実装例。
//
// OnEnter で全基盤（GameCore / Bridge / Character / UI / 方針）を CompositionScope に
// 組み立て、OnExit で逆順に片付けて舞台のGameObjectごと破棄する。
// ステージ（payload = StageId.Value）はマスターデータで差し替わる——
// 地面の色・広さ・敵の攻撃間隔がステージ仕様から流れ込む。
//
// ステージ切り替え＝このフェーズへ別の StageId で再入するだけ
// （GameFlow が同一フェーズでも Exit→Enter を完全に回す）。
// ============================================================================

using System.Collections.Generic;
using Game.Battle.Contracts;
using Seed.AI;
using Seed.Character;
using Seed.Core.Presenter;
using Seed.Core.Samples.ActionBattle;
using Seed.Data;
using Seed.Flow;
using Seed.Hub;
using Seed.Hub.Contracts;
using Seed.Input;
using Seed.Cameras;
using Seed.Motion;
using Seed.Pooling;
using Seed.World;
using Seed.StageGen;
using Seed.UI;
using UnityEngine;

namespace Seed.App
{
    /// <summary>【サンプル】戦闘フェーズ（旧統合デモの戦闘合成をフェーズ化したもの）。</summary>
    public sealed class Sample_BattlePhase : GamePhase
    {
        /// <summary>Actorキー: 3Dモデル表現。</summary>
        private static readonly ActorKey ModelActor = new ActorKey(1);

        /// <summary>Actorキー: 2D立ち絵表現。</summary>
        private static readonly ActorKey PortraitActor = new ActorKey(2);

        // ---- フェーズをまたいで生きる依存（永続ルートが注入） ----

        /// <summary>仲介基盤: メッセージハブ。</summary>
        private readonly MessageHub _hub;

        /// <summary>仲介基盤: サービス台帳。</summary>
        private readonly ServiceRegistry _services;

        /// <summary>入力（永続ルートが所有。ここでは読むだけ）。</summary>
        private readonly InputRouter _input;

        /// <summary>マスターデータ（ユニット仕様＋ステージ仕様）。</summary>
        private readonly MasterDataSet _catalog;

        // ---- このフェーズ滞在中だけ生きるもの（OnEnter で組み、OnExit で全部消える） ----

        /// <summary>破棄対象の台帳。</summary>
        private CompositionScope _scope;

        /// <summary>1フレームの実行順。</summary>
        private TickPipeline _pipeline;

        /// <summary>舞台の表示物すべての親（OnExit で丸ごと破棄）。</summary>
        private GameObject _stageRoot;

        /// <summary>GameCore側の世界。</summary>
        private Sample_ActionWorld _world;

        /// <summary>GameCore⇔Hubの翻訳者。</summary>
        private CoreHubBridge _bridge;

        /// <summary>キャラクター基盤: 全体管理。</summary>
        private CharactersManager _characters;

        /// <summary>キャラクター基盤: プレイヤー陣営。</summary>
        private PlayersManager _players;

        /// <summary>キャラクター基盤: エネミー陣営。</summary>
        private EnemiesManager _enemies;

        /// <summary>プレイヤーの制御（入力の接続先）。</summary>
        private PlayerController _playerController;

        /// <summary>リザルト画面（決着テキストの差し込み先）。</summary>
        private Sample_ResultScreen _resultScreen;

        /// <summary>時間基盤の窓口（ポーズ判定とゲームdtの取得）。</summary>
        private IGameClock _clock;

        /// <summary>この戦闘のメタAI（攻撃権の采配・手心）。</summary>
        private Sample_BattleDirector _director;

        /// <summary>自動操縦の頭脳（プレイヤーユニット用。敵と同じ思考候補を使う）。</summary>
        private AiBrain _playerBrain;

        /// <summary>自動操縦の入力注入先（AI→ManualLogic の橋）。</summary>
        private InputEmulator _playerEmulator;

        /// <summary>自動操縦中か（[O] で切替）。</summary>
        private bool _isAutopilot;

        /// <summary>姿勢デモ: プレイヤーの頭の注視リグ（敵を目で追う）。</summary>
        private LookAtRig _playerLook;

        /// <summary>IKデモ: プレイヤーの左腕2ボーンIK（近づくと敵へ手を伸ばす）。</summary>
        private TwoBoneIkRig _playerArm;

        /// <summary>足IKデモ: プレイヤーの両足（階段・坂で接地させる）。</summary>
        private FootIkRig _playerFeet;

        /// <summary>実モデル（UnityChan）。無い環境では null＝カプセルで代替。</summary>
        private Sample_PlayerModel _playerModel;

        /// <summary>プレイヤーの移動モーター（重力・ジャンプ・段差を一手に担う）。</summary>
        private Sample_KinematicMotor _playerMotor;

        /// <summary>この戦闘の体（エージェント×モーター。dt供給と待機中重力の対象）。</summary>
        private readonly List<(CharacterAgent Agent, Sample_KinematicMotor Motor)> _bodies =
            new List<(CharacterAgent, Sample_KinematicMotor)>();

        /// <summary>揺れものが有効か（[4] で切替。無し状態のサンプル比較用）。</summary>
        private bool _springsEnabled = true;

        /// <summary>カメラの水平角（度。マウスで回す）。</summary>
        private float _cameraYaw;

        /// <summary>カメラの縦角（度。見下ろし正）。</summary>
        private float _cameraPitch;

        /// <summary>走りクリップを流しているか（歩き⇄走りの往復を防ぐヒステリシス）。</summary>
        private bool _runClipActive;

        /// <summary>スムージング済みの移動方向（入力の角がそのまま体に出ないようにする）。</summary>
        private Vector3 _moveDirection;

        /// <summary>スムージング済みの移動強度 0〜1（加速・減速のなめらかさの源）。</summary>
        private float _moveBlend;

        /// <summary>スムージング済みの視点入力（マウスの粗い刻みを丸める）。</summary>
        private Vector2 _lookSmoothed;

        /// <summary>討伐成功したか（勝利ポーズの維持に使う）。</summary>
        private bool _victory;

        /// <summary>「物を拾う」インタラクション（台帳＋状態機械。腕IKの実演）。</summary>
        private Sample_PickupInteraction _pickup;

        /// <summary>プレイヤーの左手（拾ったアイテムの吸着先）。</summary>
        private Transform _playerHandBone;

        /// <summary>オーブの共有マテリアル（発光。OnExit で破棄する）。</summary>
        private Material _pickupMaterial;

        /// <summary>オーブの浮遊高さ（m。立ったまま左手が届く腰〜胸の高さ）。</summary>
        private const float PickupOrbHeight = 0.85f;

        /// <summary>オーブの直径スケール。</summary>
        private const float PickupOrbScale = 0.22f;

        /// <summary>オーブ浮遊アニメの経過時間（時間基盤のdt積算＝スロー・ポーズが効く）。</summary>
        private float _pickupClock;

        /// <summary>平滑済みの注視点（拾う⇄敵リーチの切替で首が跳ねないように）。</summary>
        private Vector3 _lookPoint;

        /// <summary>_lookPoint が有効か（無効なら次の目標へ即時一致）。</summary>
        private bool _lookPointValid;

        /// <summary>平滑済みの注視重み。</summary>
        private float _lookWeightBlend;

        /// <summary>平滑済みの腕IK目標。</summary>
        private Vector3 _armPoint;

        /// <summary>_armPoint が有効か。</summary>
        private bool _armPointValid;

        /// <summary>平滑済みの腕IK重み。</summary>
        private float _armWeightBlend;

        /// <summary>FPS視点の追従先（頭）。</summary>
        private Transform _playerHead;

        /// <summary>TPS視点の追従先（腰の高さのダミー）。</summary>
        private Transform _cameraPivot;

        /// <summary>視点の采配（Cinemachine のカメラを視点IDで指名する）。</summary>
        private CameraDirector _cameraDirector;

        /// <summary>[C] の巡回位置。</summary>
        private int _viewpointIndex;

        /// <summary>原点回帰（広いフィールドでの座標精度の維持）。</summary>
        private OriginShiftSystem _originShift;

        /// <summary>オブジェクトプールの台帳（ヒットエフェクトの使い回し）。</summary>
        private PoolRegistry _pools;

        /// <summary>ヒットエフェクトの複製元（非表示のテンプレート）。</summary>
        private GameObject _hitEffectPrefab;

        /// <summary>表示中のエフェクト（残り時間つき。0 になったらプールへ返す）。</summary>
        private readonly List<(GameObject Instance, float Remain)> _liveEffects =
            new List<(GameObject, float)>();

        /// <summary>フィールドのトリガー1件（出口・ショップ・宝箱。純C#の距離判定で発火）。</summary>
        private sealed class FieldTrigger
        {
            /// <summary>種別。</summary>
            public PlacementKind Kind;

            /// <summary>参照ID。</summary>
            public int RefId;

            /// <summary>ワールド位置。</summary>
            public Vector3 Position;

            /// <summary>目印の表示物（消費時に消す）。</summary>
            public GameObject Marker;
        }

        /// <summary>このステージのトリガー（生成ステージで配置される）。</summary>
        private readonly List<FieldTrigger> _triggers = new List<FieldTrigger>();

        /// <summary>バイオーム: 草原（迷宮の入口側）。</summary>
        private static readonly BiomeId GrassBiome = new BiomeId(1);

        /// <summary>バイオーム: 溶岩（迷宮の最奥側）。</summary>
        private static readonly BiomeId LavaBiome = new BiomeId(2);

        /// <summary>バイオーム: 住宅街（市街）。</summary>
        private static readonly BiomeId ResidentialBiome = new BiomeId(3);

        /// <summary>バイオーム: 市場（市街。ショップはここに立つ）。</summary>
        private static readonly BiomeId MarketBiome = new BiomeId(4);

        /// <summary>プレイヤーのID。</summary>
        private CharacterId _playerId;

        /// <summary>敵のID。</summary>
        private CharacterId _enemyId;

        /// <summary>ロジック時間の端数繰り越し（float秒→ms の境界変換）。</summary>
        private LogicTimeAccumulator _logicTime;

        /// <summary>決着済みか。</summary>
        private bool _isOver;

        /// <summary>Sample_BattlePhase を生成する。</summary>
        public Sample_BattlePhase(MessageHub hub, ServiceRegistry services,
            InputRouter input, MasterDataSet catalog)
        {
            _hub = hub;
            _services = services;
            _input = input;
            _catalog = catalog;
        }

        /// <summary>このフェーズのID。</summary>
        public override PhaseId Id => Sample_PhaseIds.Battle;

        /// <summary>
        /// 戦闘の合成ルート（payload = StageId.Value）。
        /// シーンアセットを使うステージなら、ここではなく CreateLoadOperation で
        /// ISceneLoader.LoadScene を返し、ロード完了後に本メソッドが呼ばれる。
        /// </summary>
        public override void OnEnter(int payload)
        {
            var stage = ResolveStage(payload);
            _scope = new CompositionScope();
            _isOver = false;
            _logicTime.Reset();
            _clock = _services.Resolve<IGameClock>();

            // 1. GameCore（ロジック）とBridge。シードはステージごとに変える
            _world = new Sample_ActionWorld((uint)(700 + stage.Id), trace: null);
            _playerId = new CharacterId(_world.Registry.GetId(_world.Hunter));
            _enemyId = new CharacterId(_world.Registry.GetId(_world.Monster));
            _bridge = _scope.Own(new CoreHubBridge(_world, _hub));
            _bridge.Journal.Seed = (uint)(700 + stage.Id);
            _bridge.Initialize();

            // 2. キャラクター基盤
            var registry = new CharacterRegistry();
            _characters = new CharactersManager(registry);
            _players = new PlayersManager(registry);
            _enemies = new EnemiesManager(registry);
            _characters.AddManager(_players);
            _characters.AddManager(_enemies);
            var characterSystem = _scope.Own(new CharacterSystem());
            characterSystem.Initialize(_hub, _services, _characters);

            // 3. UI基盤と方針
            var router = new ScreenRouter();
            var uiSystem = _scope.Own(new UISystem());
            uiSystem.Initialize(_hub, router, _services);
            var policy = _scope.Own(new Sample_ReactionPolicy());
            policy.Initialize(_hub);

            // 4. メタAI（采配ルール。ユニットの思考はこの指示書を読む）
            _director = new Sample_BattleDirector(_enemyId, _playerId, stage.EnemyAttackInterval);
            _isAutopilot = false;

            // 5. 舞台とユニットと画面（すべて _stageRoot の下＝OnExit で丸ごと消える）
            _stageRoot = new GameObject($"Stage_{stage.DebugName}");
            BuildStage(stage);
            BuildCameras();
            BuildPools();
            BuildOriginShift();
            BuildScreens(uiSystem, stage);
            _hub.PublishCommand(new ShowScreenCommand(Sample_ScreenIds.BattleHud));

            // 6. 1フレームの実行順（メタAIの采配 → ユニットの思考、の順を登録順で保証）
            _pipeline = new TickPipeline();
            _pipeline.Add(TickPhase.Input, HandlePlayerInput);
            _pipeline.Add(TickPhase.Simulation, dt => _director.Tick(dt));
            _pipeline.Add(TickPhase.Simulation, dt =>
            {
                for (var i = 0; i < _bodies.Count; i++)
                {
                    _bodies[i].Motor.PreTick(dt);
                }
            });
            _pipeline.Add(TickPhase.Simulation, dt => _characters.Tick(dt));
            _pipeline.Add(TickPhase.Simulation, _ => ApplyIdleGravity());
            _pipeline.Add(TickPhase.Simulation, dt => TickPickup(dt));
            _pipeline.Add(TickPhase.LogicTime, AdvanceLogicTime);
            _pipeline.Add(TickPhase.Drain, _ => _bridge.Drain());
            _pipeline.Add(TickPhase.Drain, _ => CheckBattleEnd());
            _pipeline.Add(TickPhase.Drain, _ => CheckTriggers());
            _pipeline.Add(TickPhase.Simulation, _ => _originShift.Tick());
            _pipeline.Add(TickPhase.Drain, dt => AnimatePickups(dt));
            _pipeline.Add(TickPhase.Drain, _ => UpdateCameraPivot());
            _pipeline.Add(TickPhase.Drain, _ => UpdateDemoRig());
            _pipeline.Add(TickPhase.Drain, UpdateEffects);
            _pipeline.Add(TickPhase.Drain, _ => uiSystem.Tick(_clock.UnscaledDelta));

            // 被弾の瞬間だけ世界を止める（ヒットストップ）——時間基盤への命令1発で全基盤に効く
            // TPS 標準のマウス視点のため、戦闘中はカーソルをロックする（[B] 帰還で戻る）
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            _moveDirection = Vector3.zero;
            _moveBlend = 0f;
            _lookSmoothed = Vector2.zero;
            _runClipActive = false;
            _victory = false;
            _springsEnabled = true;
            ApplyAtmosphere();

            var battleSubscriptions = _scope.Own(new SubscriptionBag(1));
            _hub.Subscribe<CharacterDamaged>(message =>
            {
                _hub.PublishCommand(new HitStopCommand(0.06f));
                SpawnHitEffect(message.Target);
                if (message.Target.Equals(_playerId))
                {
                    // メタAIの手心の入力（プレイヤーが弱るほど攻撃間隔が延びる）
                    _director.NotifyPlayerHp(message.RemainingHp, message.MaxHp);
                }
            }).AddTo(battleSubscriptions);
        }

        /// <summary>毎フレームの駆動と「ホームへ戻る」入力。</summary>
        public override void Tick(float deltaTime)
        {
            // [B] はいつでも受け付ける（決着後・ポーズ中の帰り道でもある）
            if (_input.WasPressedThisFrame(ActionId.Cancel))
            {
                _hub.PublishCommand(new ChangePhaseCommand(Sample_PhaseIds.Home));
                return;
            }
            // [P] ポーズ切替（時間基盤への命令。処理者は GameClock のみ）
            if (_input.WasPressedThisFrame(ActionId.Previous))
            {
                _hub.PublishCommand(new SetPausedCommand(!_clock.IsPaused));
            }
            if (!_isOver && !_clock.IsPaused)
            {
                // dt は時間基盤のゲームdt——ヒットストップ・スローモーションが世界全体へ効く
                _pipeline.Tick(_clock.ScaledDelta);
            }
        }

        /// <summary>組み立ての逆順で片付け、舞台を丸ごと破棄する。</summary>
        public override void OnExit()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            RenderSettings.fog = false;
            _triggers.Clear();
            _playerModel = null;
            _playerMotor = null;
            _bodies.Clear();
            _pickup?.Clear();
            _pickup = null;
            _playerHandBone = null;
            _lookPointValid = false;
            _armPointValid = false;
            _lookWeightBlend = 0f;
            _armWeightBlend = 0f;
            if (_pickupMaterial != null)
            {
                Object.Destroy(_pickupMaterial);
                _pickupMaterial = null;
            }
            _liveEffects.Clear();
            _pools?.Clear();
            _pools = null;
            _playerFeet = null;
            _cameraDirector = null;
            _originShift = null;
            _scope?.Dispose();
            _characters?.Clear();
            if (_stageRoot != null)
            {
                Object.Destroy(_stageRoot);
                _stageRoot = null;
            }
        }

        /// <summary>荷物からステージ仕様を引く（未指定・不明はステージ1に倒す）。</summary>
        private Sample_StageSpec ResolveStage(int payload)
        {
            if (payload != 0 && _catalog.TryGet<Sample_StageSpec>(payload, out var spec))
            {
                return spec;
            }
            return _catalog.Get<Sample_StageSpec>(Sample_MasterCatalog.Stage1.Value);
        }

        // ================================================================
        // Input / LogicTime / Drain フェーズ
        // ================================================================

        /// <summary>入力を ManualLogic へ保管する（Router の Tick は永続ルートが1回だけ行う）。</summary>
        private void HandlePlayerInput(float deltaTime)
        {
            var manual = _playerController.Manual;

            // [C] 視点切替（TPS→FPS→俯瞰の巡回）。カメラ基盤への命令1発で、
            // 発行側は Cinemachine のカメラ構成を知らない
            if (_input.WasPressedThisFrame(Sample_ActionIds.CycleView))
            {
                _viewpointIndex = (_viewpointIndex + 1) % 3;
                var next = _viewpointIndex == 0 ? ViewpointId.ThirdPerson
                    : _viewpointIndex == 1 ? ViewpointId.FirstPerson
                    : ViewpointId.Overhead;
                _hub.PublishCommand(new SetViewpointCommand(next, blendSeconds: 0.35f));
            }

            // マウス視点（自動操縦中も有効＝観戦カメラとして回せる）。
            // 生のマウスdeltaは刻みが粗いので軽く均す（フレームレート非依存の指数補間）
            var look = _input.Look;
            _lookSmoothed = Vector2.Lerp(_lookSmoothed, look,
                1f - Mathf.Exp(-25f * deltaTime));
            _cameraYaw += _lookSmoothed.x * 0.12f;
            _cameraPitch = Mathf.Clamp(_cameraPitch - _lookSmoothed.y * 0.10f, -30f, 65f);

            // [O] 自動操縦の切替（プレイヤーとAIの制御共通化のデモ）
            if (_input.WasPressedThisFrame(Sample_ActionIds.Autopilot))
            {
                _isAutopilot = !_isAutopilot;
                if (!_isAutopilot)
                {
                    _playerEmulator.ReleaseAll(); // 人間の入力へ返す
                }
            }

            // [T] 3D⇔2D Actor切替（見た目の切替であり操縦者と無関係——自動操縦中も有効）
            if (_input.WasPressedThisFrame(ActionId.Next))
            {
                var agent = _playerController.Agent;
                var next = agent.ActiveActor.Key.Equals(ModelActor) ? PortraitActor : ModelActor;
                agent.SwitchActor(next);
            }

            // [4] 揺れものON/OFF——「揺れもの無しの3Dサンプル」状態と見比べるための切替。
            // Weight=0 は書き戻しだけ止めてシミュは続ける仕様＝再ONでも不連続にならない
            if (_input.WasPressedThisFrame(Sample_ActionIds.Slot4) && _playerModel != null)
            {
                _springsEnabled = !_springsEnabled;
                for (var i = 0; i < _playerModel.Springs.Count; i++)
                {
                    _playerModel.Springs[i].Weight = _springsEnabled ? 1f : 0f;
                }
                Debug.Log($"[Motion] 揺れもの: {(_springsEnabled ? "ON" : "OFF")}");
            }

            if (_isAutopilot)
            {
                // AIの意図を「入力として」注入する。Controller/Agent/Behavior は
                // 手動時と1行も変わらない——変わるのは ManualLogic へ書く者だけ
                var agent = _playerController.Agent;
                var pose = agent.ActiveActor.Pose;
                var frame = new LogicFrame(_playerId, pose.Position, pose.Rotation,
                    agent.IsAlive, deltaTime);
                _playerEmulator.Drive(_playerBrain.Think(in frame));
                return; // 物理キーの移動・攻撃・ガードは読まない（[T]は上で処理済み、[B][P]は Tick 側で有効）
            }

            // 移動はカメラ相対（TPSの標準操作。W=カメラの向く先へ進む）。
            // デジタル入力（0か1）を直結すると発進・停止・方向転換が角ばるため、
            // **強度と向きを別々に補間**して体の動きへ渡す（Starter Assets と同じ骨子。
            // LocomotionBehavior は意図の大きさ 0〜1 を速度スケールとして解釈する）
            var move = _input.Move;
            var hasInput = move.sqrMagnitude > 0.01f;
            var targetBlend = !hasInput ? 0f
                : _input.IsPressed(Sample_ActionIds.Walk) ? 0.45f : 1f; // [Shift]=歩き
            _moveBlend = Mathf.Lerp(_moveBlend, targetBlend,
                1f - Mathf.Exp(-8f * deltaTime));
            if (hasInput)
            {
                var rawDirection = (Quaternion.Euler(0f, _cameraYaw, 0f)
                    * new Vector3(move.x, 0f, move.y)).normalized;
                // 停止からの入力は即その向き・移動中の切り返しはなめらかに曲がる
                _moveDirection = _moveDirection.sqrMagnitude < 0.01f
                    ? rawDirection
                    : Vector3.Slerp(_moveDirection, rawDirection,
                        1f - Mathf.Exp(-12f * deltaTime));
            }
            manual.SetMove(_moveBlend < 0.02f ? Vector3.zero : _moveDirection * _moveBlend);

            // [Space] ジャンプ（接地中のみ。滞空クリップは UpdatePlayerAnimation が面倒を見る）
            if (_input.WasPressedThisFrame(ActionId.Jump))
            {
                _playerMotor?.RequestJump();
            }

            // [2] 拾う——近くのオーブへ左手を伸ばす（腕IK＝TwoBoneIkRig の実演）
            if (_input.WasPressedThisFrame(ActionId.Interact))
            {
                _pickup?.TryStart(_playerController.Agent.ActiveActor.Pose.Position);
            }

            manual.SetGuard(_input.IsPressed(ActionId.Guard));
            if (_input.WasPressedThisFrame(ActionId.Guard) || _input.WasReleasedThisFrame(ActionId.Guard))
            {
                _hub.Publish(new GuardInputChanged(_playerId, _input.IsPressed(ActionId.Guard)));
            }

            if (_input.WasPressedThisFrame(ActionId.Attack))
            {
                manual.RequestAction(BehaviorKey.Attack, _world.Registry.GetId(_world.SlashUp));
            }
        }

        /// <summary>GameCoreのロジック時間を進める（時間前進も入力としてジャーナルに乗る）。</summary>
        private void AdvanceLogicTime(float deltaTime)
        {
            var ms = _logicTime.Advance(deltaTime);
            if (ms <= 0)
            {
                return;
            }
            _bridge.AdvanceTime(ms);
        }

        /// <summary>決着を監視し、リザルトのテキストを確定する。</summary>
        private void CheckBattleEnd()
        {
            if (!_world.Hunter.IsDead && !_world.Monster.IsDead)
            {
                return;
            }
            _isOver = true;
            _victory = _world.Monster.IsDead;
            // 決着でパイプラインは止まるが MotionRig の LateUpdate は動き続ける——
            // 拾いかけの腕・縮みかけのオーブが画面に残らないよう即座に畳む
            _pickup?.ForceFinish();
            _playerArm?.ClearTarget();
            _playerLook?.ClearTarget();
            var result = _victory ? "討伐成功！" : "力尽きた…";
            _resultScreen.SetResult($"{result}   [B] ホームへ");
            // 画面遷移は方針クラスが CharacterDied → ShowScreenCommand(Result) で発行済み
        }

        // ================================================================
        // 舞台とユニットの構築（すべて _stageRoot の下）
        // ================================================================

        /// <summary>地面・ユニット2体をステージ仕様どおりに生成する。</summary>
        private void BuildStage(Sample_StageSpec stage)
        {
            if (stage.GeneratorKind != 0)
            {
                BuildGeneratedStage(stage);
                return;
            }

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(_stageRoot.transform, false);
            ground.transform.localScale = new Vector3(stage.GroundScale, 1f, stage.GroundScale);
            RendererTint.Set(ground.GetComponent<Renderer>(), stage.GroundColor);

            BuildTerrainFeatures();
            BuildPickups();

            BuildPlayerUnit(_catalog.Get<Sample_UnitSpec>(_playerId.Value), new Vector3(0f, 1f, -3f),
                attachBody: false);
            BuildEnemyUnit(_catalog.Get<Sample_UnitSpec>(_enemyId.Value), stage, new Vector3(0f, 1.5f, 2f),
                attachBody: false);

            // 移動の解決（重力・段差・ジャンプ）は BuildPlayerUnit が装着する移動モーターが担う
        }

        // ================================================================
        // 自動生成ステージ（Seed.StageGen）
        // ================================================================

        /// <summary>
        /// 自動生成ステージの構築（生成→施工→配置の実体化）。
        /// 設計図はシードから決定的に生成されるため、同じステージへ再入すれば同じ地形・同じ配置になる。
        /// </summary>
        private void BuildGeneratedStage(Sample_StageSpec stage)
        {
            // 1. 生成（純C#）: ステージ種別ごとのパイプラインで設計図を作る
            var seed = (uint)(700 + stage.Id);
            var blueprint = (stage.GeneratorKind == 1
                    ? BuildMazePipeline(stage)
                    : BuildTownPipeline(stage))
                .Generate(stage.GenWidth, stage.GenHeight, cellSize: 1.5f, seed);

            // 2. 施工: パレット（素材登録）＋配置表（意味の実体化）
            //    美術アセットが入ったら Bind の行をプレハブ Instantiate に差し替えるだけ
            var builder = new StageBuilder(BuildPalette(stage))
                .SetPlacement(PlacementKind.PlayerSpawn, (in Placement _, Vector3 __, Transform ___) => { })
                .SetPlacement(PlacementKind.EnemySpawn, (in Placement _, Vector3 __, Transform ___) => { })
                .SetPlacement(PlacementKind.Exit, MakeTrigger(new Color(0.2f, 0.9f, 0.4f)))
                .SetPlacement(PlacementKind.Shop, MakeTrigger(new Color(0.95f, 0.85f, 0.2f)))
                .SetPlacement(PlacementKind.Chest, MakeTrigger(new Color(0.75f, 0.4f, 0.9f)));
            builder.Build(blueprint, _stageRoot.transform);

            // 3. ユニット: 設計図のスポーン点へ配置（生成ステージは壁があるので当たりを装着）
            blueprint.TryFindPlacement(PlacementKind.PlayerSpawn, out var playerSpawn);
            var playerPosition = blueprint.GridToWorld(playerSpawn.X, playerSpawn.Y) + Vector3.up;
            BuildPlayerUnit(_catalog.Get<Sample_UnitSpec>(_playerId.Value), playerPosition,
                attachBody: true);

            // 既知の制約: GameCoreサンプル世界が Hunter/Monster の1v1固定のため、
            // 実体化する敵は最初の EnemySpawn の1体だけ（配置基盤側は複数・テーブル対応済み。
            // ロジックをN体対応にすれば、ここの実体化を回すだけで複数化できる）
            if (blueprint.TryFindPlacement(PlacementKind.EnemySpawn, out var enemySpawn))
            {
                var enemyPosition = blueprint.GridToWorld(enemySpawn.X, enemySpawn.Y)
                    + new Vector3(0f, 1.5f, 0f);
                BuildEnemyUnit(_catalog.Get<Sample_UnitSpec>(_enemyId.Value), stage, enemyPosition,
                    attachBody: true);
            }

            // 4. カメラ: ステージ寸法に合わせた俯瞰
            var camera = Camera.main;
            if (camera != null)
            {
                var extent = Mathf.Max(stage.GenWidth, stage.GenHeight) * 1.5f;
                camera.transform.position = new Vector3(0f, extent * 0.95f, -extent * 0.45f);
                camera.transform.LookAt(Vector3.zero);
            }
        }

        /// <summary>迷宮のパイプライン（迷路＋手作り部屋＋距離帯バイオーム＋配置）。</summary>
        private GenerationPipeline BuildMazePipeline(Sample_StageSpec stage)
        {
            // 手作りの部屋テンプレート（文字列で書ける「単位での登録」。宝箱を内包した宝物庫）
            var legend = new RoomTemplateLegend()
                .Cell('#', CellType.Wall)
                .Cell('.', CellType.Floor)
                .Door('D')
                .Placement('C', CellType.Floor, PlacementKind.Chest, refId: 1);
            var vault = RoomTemplate.Parse(new[]
            {
                "#####",
                "#C.C#",
                "#...D",
                "#####",
            }, legend);
            var hall = RoomTemplate.Parse(new[]
            {
                "##D##",
                "#...#",
                "D...D",
                "#...#",
                "##D##",
            }, legend);

            return new GenerationPipeline()
                .Add(new FillPass(CellType.Wall))
                .Add(new MazeCarvePass(stage.BraidPermille))
                .Add(new TemplateRoomsPass(new[] { vault, hall }))
                .Add(new PlayerSpawnPass())
                .Add(BiomeAssignPass.DistanceBands(
                    new BiomeAssignPass.Band(500, GrassBiome),
                    new BiomeAssignPass.Band(1000, LavaBiome)))
                .Add(new TileVariantPass(new TileVariantTable()
                    .Add(GrassBiome, CellType.Floor, 700, 300)
                    .Add(LavaBiome, CellType.Floor, 300, 700)
                    .Add(BiomeId.None, CellType.Wall, 800, 200)))
                .Add(new EnemyPlacementPass(1, 500, 1000,
                    new PlacementTable().Add(_enemyId.Value, 1000)))
                .Add(new LandmarkPlacementPass(PlacementKind.Exit, 0, LandmarkRule.Farthest))
                .Add(new LandmarkPlacementPass(PlacementKind.Chest, 1, LandmarkRule.RandomWalkable));
        }

        /// <summary>市街のパイプライン（BSP区画＋建物＋区画バイオーム＋店・出口）。</summary>
        private GenerationPipeline BuildTownPipeline(Sample_StageSpec stage)
        {
            return new GenerationPipeline()
                .Add(new FillPass(CellType.Floor))
                .Add(new BspDistrictPass(minDistrictSize: 7))
                .Add(new BuildingPlacementPass())
                .Add(BiomeAssignPass.RegionBased(ResidentialBiome, MarketBiome))
                .Add(new TileVariantPass(new TileVariantTable()
                    .Add(BiomeId.None, CellType.Building, 600, 400)))
                .Add(new PlayerSpawnPass())
                .Add(new EnemyPlacementPass(1, 400, 1000,
                    new PlacementTable().Add(_enemyId.Value, 1000)))
                .Add(new LandmarkPlacementPass(PlacementKind.Shop, 0, LandmarkRule.RegionCenter,
                    MarketBiome))
                .Add(new LandmarkPlacementPass(PlacementKind.Exit, 0, LandmarkRule.Farthest));
        }

        /// <summary>
        /// アセットパレットを組む（床や壁などの複数素材登録の実演）。
        /// 本デモは色違いプリミティブを「素材」として登録している——
        /// プレハブが出来たら同じ Bind でプレハブ工場に差し替えるだけで、生成側は一切変わらない。
        /// </summary>
        private static StageAssetPalette BuildPalette(Sample_StageSpec stage)
        {
            return new StageAssetPalette()
                // 迷宮: 草原帯（床2バリアント）
                .Bind(GrassBiome, CellType.Floor, 0, PrimitiveTiles.FlatTile(new Color(0.4f, 0.62f, 0.34f)))
                .Bind(GrassBiome, CellType.Floor, 1, PrimitiveTiles.FlatTile(new Color(0.33f, 0.55f, 0.3f)))
                // 迷宮: 溶岩帯（床2バリアント）
                .Bind(LavaBiome, CellType.Floor, 0, PrimitiveTiles.FlatTile(new Color(0.62f, 0.3f, 0.2f)))
                .Bind(LavaBiome, CellType.Floor, 1, PrimitiveTiles.FlatTile(new Color(0.5f, 0.2f, 0.15f)))
                // 壁2バリアント（バイオーム不問）
                .Bind(BiomeId.None, CellType.Wall, 0, PrimitiveTiles.BlockTile(new Color(0.35f, 0.35f, 0.42f)))
                .Bind(BiomeId.None, CellType.Wall, 1, PrimitiveTiles.BlockTile(new Color(0.28f, 0.28f, 0.34f)))
                // 市街: 区画バイオームで建物の色を変える
                .Bind(ResidentialBiome, CellType.Building, 0, PrimitiveTiles.BlockTile(new Color(0.55f, 0.42f, 0.3f), 3f))
                .Bind(ResidentialBiome, CellType.Building, 1, PrimitiveTiles.BlockTile(new Color(0.48f, 0.36f, 0.26f), 2.5f))
                .Bind(MarketBiome, CellType.Building, 0, PrimitiveTiles.BlockTile(new Color(0.35f, 0.45f, 0.6f), 3f))
                .Bind(MarketBiome, CellType.Building, 1, PrimitiveTiles.BlockTile(new Color(0.3f, 0.4f, 0.52f), 2.5f));
        }

        /// <summary>トリガー付き目印の実体化工場を作る（出口・ショップ・宝箱）。</summary>
        private PlacementFactory MakeTrigger(Color color)
        {
            return (in Placement placement, Vector3 position, Transform parent) =>
            {
                var marker = PrimitiveTiles.MarkerTile(color)(position, 1.5f, parent);
                _triggers.Add(new FieldTrigger
                {
                    Kind = placement.Kind,
                    RefId = placement.RefId,
                    Position = position,
                    Marker = marker,
                });
            };
        }

        /// <summary>
        /// トリガーの踏み判定（純C#の距離判定＝コライダー不要で決定的）。
        /// 出口→帰還 / ショップ→店へ / 宝箱→鬼人薬（入力としてジャーナルに乗る）。
        /// </summary>
        private void CheckTriggers()
        {
            if (_triggers.Count == 0)
            {
                return;
            }
            var playerPosition = _playerController.Agent.ActiveActor.Pose.Position;
            for (var i = _triggers.Count - 1; i >= 0; i--)
            {
                var trigger = _triggers[i];
                var delta = trigger.Position - playerPosition;
                delta.y = 0f;
                if (delta.sqrMagnitude > 0.8f * 0.8f)
                {
                    continue;
                }

                if (trigger.Kind.Equals(PlacementKind.Exit))
                {
                    _hub.PublishCommand(new ChangePhaseCommand(Sample_PhaseIds.Home));
                    return;
                }
                if (trigger.Kind.Equals(PlacementKind.Shop))
                {
                    _hub.PublishCommand(new ChangePhaseCommand(Sample_PhaseIds.Shop));
                    return;
                }
                if (trigger.Kind.Equals(PlacementKind.Chest))
                {
                    _bridge.UseDemonDrug(attackBonus: 15, durationMs: 20000);
                    if (trigger.Marker != null)
                    {
                        Object.Destroy(trigger.Marker);
                    }
                    _triggers.RemoveAt(i); // 宝箱は1回で消える
                }
            }
        }

        /// <summary>プレイヤーユニットを組み立てる（3Dモデル＋2D立ち絵の2表現）。</summary>
        private void BuildPlayerUnit(Sample_UnitSpec spec, Vector3 spawn, bool attachBody)
        {
            // 実モデル（Humanoid）があれば RiggedAvatar、無ければ従来のカプセル
            _playerModel = Sample_PlayerModel.TryCreate(_stageRoot.transform);
            var modelAvatar = _playerModel != null
                ? (IAvatar)_playerModel.Avatar
                : CreateModelAvatar("Player", PrimitiveType.Capsule, new Color(0.25f, 0.45f, 0.9f));
            if (_playerModel != null)
            {
                // モデルの原点は足元（カプセルは中心）なので、スポーン高さを地面へ合わせる
                spawn = new Vector3(spawn.x, spawn.y - 1f, spawn.z);
            }

            var agent = CharacterFactory.Create(_playerId, spec.ToDefinition(),
                new ActorBlueprint(ModelActor, modelAvatar),
                new ActorBlueprint(PortraitActor,
                    CreatePortraitAvatar("Player2D", new Color(0.35f, 0.55f, 1f))));
            SetSpawnPose(agent, spawn, Quaternion.identity);

            // 移動モーター（重力・ジャンプ・段差）。平地・生成ステージ問わず常に装着する
            if (agent.ActiveActor.Avatar is Component avatarComponent)
            {
                // 足元レイキャスト（足IK・カメラ衝突）が自分自身に当たらないようにする
                SetLayerRecursive(avatarComponent.gameObject, 2); // Ignore Raycast
                var bodyHeight = _playerModel != null ? Mathf.Max(1f, _playerModel.Height) : 1.6f;
                _playerMotor = new Sample_KinematicMotor(avatarComponent.gameObject, bodyHeight,
                    originAtFeet: _playerModel != null);
                _bodies.Add((agent, _playerMotor));
                agent.ActiveActor.MotionSolver = _playerMotor;
            }
            agent.BehaviorStarted += OnBehaviorStarted;

            AttachDemoMotionRig(agent);

            _playerController = new PlayerController(agent, new ManualLogic());
            _players.Add(_playerController);

            // 自動操縦（[O]）: 敵とまったく同じ思考候補クラスで頭脳を組み、
            // InputEmulator で ManualLogic へ「入力として」注入する＝制御共通化の実演
            var playerRegistry = _characters.Registry;
            _playerBrain = new AiBrain(playerRegistry, playerRegistry)
                .With(new Sample_ChaseConsideration(FactionId.Enemies, stopDistance: 1.8f))
                .With(new Sample_AttackConsideration(FactionId.Enemies,
                    _world.Registry.GetId(_world.SlashUp), range: 2.4f));
            _playerEmulator = new InputEmulator(_playerController.Manual);
        }

        /// <summary>エネミーユニットを組み立てる（攻撃間隔はステージ仕様から）。</summary>
        private void BuildEnemyUnit(Sample_UnitSpec spec, Sample_StageSpec stage, Vector3 spawn, bool attachBody)
        {
            var agent = CharacterFactory.Create(_enemyId, spec.ToDefinition(),
                new ActorBlueprint(ModelActor,
                    CreateModelAvatar("Enemy", PrimitiveType.Cube, new Color(0.5f, 0.45f, 0.5f))));
            SetSpawnPose(agent, spawn, Quaternion.LookRotation(Vector3.back));
            if (attachBody)
            {
                AttachBody(agent);
            }
            agent.BehaviorStarted += OnBehaviorStarted;

            // 敵の頭脳: 追跡（遠ければ近づく）＋攻撃（射程内＆メタAIの攻撃権があるとき）。
            // 攻撃間隔の采配はメタAI（Sample_BattleDirector）の指示書が握る
            var registry = _characters.Registry;
            var brain = new AiBrain(registry, registry, _director)
                .With(new Sample_ChaseConsideration(FactionId.Players, stopDistance: 2.2f))
                .With(new Sample_AttackConsideration(FactionId.Players,
                    _world.Registry.GetId(_world.TailSwipe), range: 2.8f));
            _enemies.Add(new EnemyController(agent, brain));
        }

        /// <summary>全 Actor の初期姿勢を揃える。</summary>
        private static void SetSpawnPose(CharacterAgent agent, Vector3 position, Quaternion rotation)
        {
            var active = agent.ActiveActor;
            active.Pose.Position = position;
            active.Pose.Rotation = rotation;
            active.Avatar.ApplyPose(position, rotation);
        }

        /// <summary>
        /// 敵の当たりを装着する（CapsuleCollider＋kinematic Rigidbody＋自前モーター）。
        /// Unity 標準の CharacterController は使わない（プロジェクト方針）。
        /// 生成した壁・建物が実際に移動を遮り、重力・段差も自前の
        /// collide-and-slide が解決する（プレイヤーと同じモーターの使い回し）。
        /// 2D立ち絵Actorへ切り替えている間は実体が非アクティブ＝Solver は素通しに落ちる。
        /// </summary>
        private void AttachBody(CharacterAgent agent)
        {
            if (!(agent.ActiveActor.Avatar is Component avatarComponent))
            {
                return;
            }
            // 敵（中心原点のプリミティブ）用の寸法。プレイヤーの体は BuildPlayerUnit が装着済み
            var motor = new Sample_KinematicMotor(avatarComponent.gameObject, 1.6f,
                originAtFeet: false, radius: 0.35f);
            _bodies.Add((agent, motor));
            agent.ActiveActor.MotionSolver = motor;
        }

        /// <summary>
        /// 姿勢・IKのデモリグを装着する（Seed.Motion の実演。モデルアセット無しでも動く）。
        /// 頭キューブ=注視（敵を目で追う姿勢制御）/ 左腕のプリミティブ関節=2ボーンIK /
        /// 後頭部のポニーテール=揺れもの（SpringBoneRig）。
        /// アニメの上へ LateUpdate で重なる「艶」であり、真実（Pose・行動）には触れない。
        /// </summary>
        private void AttachDemoMotionRig(CharacterAgent agent)
        {
            // 実モデル: プリミティブ関節は作らず、Humanoid ボーンへ直接リグを装着する
            if (_playerModel != null && ReferenceEquals(agent.ActiveActor.Avatar, _playerModel.Avatar))
            {
                AttachModelRig(_playerModel);
                return;
            }
            if (!(agent.ActiveActor.Avatar is Avatar3D avatar))
            {
                return;
            }
            var root = avatar.transform;

            // 腰（足IKが上下させる中心。頭・腕・脚はこの下に付けて一緒に沈む）
            var hips = CreateJoint(root, "Hips", Vector3.zero);

            // 頭（注視デモ用の小キューブ。向きが分かるよう鼻をつける）
            var head = new GameObject("Head").transform;
            head.SetParent(hips, false);
            head.localPosition = new Vector3(0f, 1.25f, 0f);
            var headCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            headCube.name = "HeadCube";
            Object.Destroy(headCube.GetComponent<Collider>());
            headCube.transform.SetParent(head, false);
            headCube.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
            var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.name = "Nose";
            Object.Destroy(nose.GetComponent<Collider>());
            nose.transform.SetParent(head, false);
            nose.transform.localPosition = new Vector3(0f, 0f, 0.2f);
            nose.transform.localScale = new Vector3(0.08f, 0.08f, 0.12f);
            RendererTint.Set(nose.GetComponent<Renderer>(), Color.black);

            // 左腕（肩→肘→手 のプリミティブ関節チェーン）
            var shoulder = CreateJoint(hips, "Shoulder", new Vector3(0.45f, 0.7f, 0f));
            var elbow = CreateJoint(shoulder, "Elbow", new Vector3(0f, -0.35f, 0f));
            var hand = CreateJoint(elbow, "Hand", new Vector3(0f, -0.35f, 0f));
            _playerHandBone = hand;

            // ポニーテール（揺れものデモ: 頭の動き・移動・旋回へ遅れて追従して揺れる）
            var tail0 = CreateJoint(head, "Tail0", new Vector3(0f, 0.05f, -0.22f));
            var tail1 = CreateJoint(tail0, "Tail1", new Vector3(0f, -0.06f, -0.16f));
            var tail2 = CreateJoint(tail1, "Tail2", new Vector3(0f, -0.06f, -0.16f));
            var tail3 = CreateJoint(tail2, "Tail3", new Vector3(0f, -0.06f, -0.16f));
            var tailParams = SpringBoneParams.Default;
            tailParams.Stiffness = 25f;                  // 柔らかめ＝遊びが見える
            tailParams.Gravity = new Vector3(0f, -3f, 0f);
            var tail = new SpringBoneRig(
                new[] { tail0, tail1, tail2, tail3 },
                tailParams,
                colliders: new[] { (head, 0.22f) });     // 頭へめり込まない

            // 脚（股→膝→足→つま先。足IKが階段・坂へ接地させる）
            var leftLeg = CreateLeg(hips, "Left", -0.14f);
            var rightLeg = CreateLeg(hips, "Right", 0.14f);
            var footSettings = new FootIkSettings
            {
                FootHeight = 0.07f,      // 足キューブの半分ぶん浮かせる
                MaxStepHeight = 0.4f,    // 1段 0.18m の階段は合わせ、壁は無視する
                MaxHipDrop = 0.3f,
                FootFollowSpeed = 4f,
                HipFollowSpeed = 2.5f,
            };
            _playerFeet = new FootIkRig(hips, leftLeg, rightLeg,
                groundMask: Physics.DefaultRaycastLayers, settings: footSettings, castRange: 1f);
            _playerHead = head;

            // リグ: 注視（頭のみ・最大70度）＋ 腕IK（肘は背中側へ曲がる）＋ 足IK ＋ 揺れもの
            _playerLook = new LookAtRig((head, 1f, 70f));
            _playerArm = new TwoBoneIkRig(shoulder, elbow, hand,
                poleHint: root.position - root.forward * 2f + Vector3.up);
            avatar.gameObject.AddComponent<MotionRig>()
                .With(_playerLook)   // 1) 頭の向き
                .With(_playerArm)    // 2) 腕IK
                .With(_playerFeet)   // 3) 足IK（腰を沈めるので揺れものより前）
                .With(tail);         // 4) 揺れは最後（注視・腰の動きに追従＝順序が意味を持つ）
        }

        /// <summary>デモ用の関節（小キューブつきの Transform）を作る。</summary>
        private static Transform CreateJoint(Transform parent, string name, Vector3 localPosition)
        {
            var joint = new GameObject(name).transform;
            joint.SetParent(parent, false);
            joint.localPosition = localPosition;
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name + "Cube";
            Object.Destroy(cube.GetComponent<Collider>());
            cube.transform.SetParent(joint, false);
            cube.transform.localScale = new Vector3(0.14f, 0.14f, 0.14f);
            return joint;
        }

        /// <summary>
        /// 実モデル（Humanoid）へ姿勢・IK・揺れもののリグを装着する。
        /// ボーンは Animator の Humanoid 対応表から引くため、モデルの階層名に依存しない。
        /// UnityChan の髪・リボン・スカート・袖はクリップに焼かれていない揺れもの専用ボーン
        /// なので、Seed.Motion の SpringBoneRig がそのまま駆動する（収集は Sample_PlayerModel）。
        /// </summary>
        private void AttachModelRig(Sample_PlayerModel model)
        {
            var root = model.Avatar.transform;
            _playerHead = model.Head; // 一人称はここへ固定される
            _playerHandBone = model.Hand; // 拾ったアイテムの吸着先

            // 注視: 首と頭へ配分すると振り向きが自然になる（首が無いリグは頭のみ）
            _playerLook = model.Neck != null
                ? new LookAtRig((model.Neck, 0.35f, 40f), (model.Head, 0.65f, 70f))
                : new LookAtRig((model.Head, 1f, 70f));

            // 左腕IK: 近距離で敵へ手を伸ばすデモ（目標は UpdateDemoRig が毎フレーム更新）
            _playerArm = new TwoBoneIkRig(model.Shoulder, model.Elbow, model.Hand,
                poleHint: root.position - root.forward * 2f + Vector3.up);

            // 足IK: 階段・坂へ接地（つま先レイつき）。腰の沈み込みは控えめにして歩きを保つ
            var footSettings = new FootIkSettings
            {
                FootHeight = 0.08f,
                MaxStepHeight = 0.4f,
                MaxHipDrop = 0.25f,
                FootFollowSpeed = 4f,
                HipFollowSpeed = 2.5f,
            };
            _playerFeet = new FootIkRig(model.Hips, model.LeftLeg, model.RightLeg,
                groundMask: Physics.DefaultRaycastLayers, settings: footSettings, castRange: 1f);

            var motionRig = root.gameObject.AddComponent<MotionRig>()
                .With(_playerLook)   // 1) 首・頭の向き
                .With(_playerArm)    // 2) 腕IK
                .With(_playerFeet);  // 3) 足IK（腰を沈める）
            for (var i = 0; i < model.Springs.Count; i++)
            {
                motionRig.With(model.Springs[i]); // 4) 揺れもの（最終姿勢に追従させるため最後）
            }
        }

        /// <summary>片脚ぶんの関節を作る（股→膝→足→つま先）。</summary>
        private static FootIkRig.Leg CreateLeg(Transform hips, string side, float sideOffset)
        {
            var hip = CreateJoint(hips, side + "Hip", new Vector3(sideOffset, -0.05f, 0f));
            var knee = CreateJoint(hip, side + "Knee", new Vector3(0f, -0.36f, 0f));
            var foot = CreateJoint(knee, side + "Foot", new Vector3(0f, -0.36f, 0f));
            var toe = CreateJoint(foot, side + "Toe", new Vector3(0f, -0.02f, 0.12f));
            return new FootIkRig.Leg(hip, knee, foot, toe);
        }

        /// <summary>
        /// 階段・坂・起伏を置く（足IKと地面吸着の実演用）。
        /// 平らな床では足IKの働きが見えないため、高さの変化を意図的に作っている。
        /// </summary>
        private void BuildTerrainFeatures()
        {
            var features = new GameObject("Terrain").transform;
            features.SetParent(_stageRoot.transform, false);

            // 階段（8段 × 0.18m）: 各段は地面まで伸ばして隙間を作らない
            const int stepCount = 8;
            const float stepHeight = 0.18f;
            const float stepDepth = 0.55f;
            for (var i = 0; i < stepCount; i++)
            {
                var height = stepHeight * (i + 1);
                CreateBlock(features, $"Step{i}",
                    new Vector3(4.5f, height * 0.5f, -1f + i * stepDepth),
                    new Vector3(3f, height, stepDepth),
                    new Color(0.55f, 0.5f, 0.45f));
            }
            var topHeight = stepHeight * stepCount;
            CreateBlock(features, "Landing",
                new Vector3(4.5f, topHeight * 0.5f, -1f + stepCount * stepDepth + 1.25f),
                new Vector3(3f, topHeight, 2.5f),
                new Color(0.6f, 0.55f, 0.5f));

            // 坂（12度）: 足裏が法線へ沿うのが見える
            var slope = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slope.name = "Slope";
            slope.transform.SetParent(features, false);
            slope.transform.localPosition = new Vector3(-4.5f, 0.35f, 0f);
            slope.transform.localRotation = Quaternion.Euler(0f, 0f, 12f);
            slope.transform.localScale = new Vector3(5.5f, 0.3f, 4.5f);
            RendererTint.Set(slope.GetComponent<Renderer>(), new Color(0.5f, 0.52f, 0.45f));

            // 起伏（片足だけ乗る低い段＝腰の沈み込みが分かる）
            CreateBlock(features, "Bump0", new Vector3(-1.2f, 0.06f, 3.2f),
                new Vector3(1.2f, 0.12f, 1.2f), new Color(0.48f, 0.5f, 0.42f));
            CreateBlock(features, "Bump1", new Vector3(0.6f, 0.1f, 4.4f),
                new Vector3(1.2f, 0.2f, 1.2f), new Color(0.5f, 0.48f, 0.4f));
            CreateBlock(features, "Bump2", new Vector3(2.2f, 0.15f, 3.0f),
                new Vector3(1.2f, 0.3f, 1.2f), new Color(0.52f, 0.46f, 0.4f));
        }

        /// <summary>色つきの箱を1つ置く（地形の部品）。</summary>
        private static void CreateBlock(Transform parent, string name, Vector3 localPosition,
            Vector3 scale, Color color)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent, false);
            block.transform.localPosition = localPosition;
            block.transform.localScale = scale;
            RendererTint.Set(block.GetComponent<Renderer>(), color);
        }

        /// <summary>
        /// カメラを組む（Cinemachine の視点3種を視点IDで指名できるようにする）。
        /// 「どう見えるか」は Cinemachine の部品が持ち、こちらは切替の入口だけを用意する。
        /// </summary>
        private void BuildCameras()
        {
            var mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return;
            }
            // カメラ注視点はキャラの子にしない（キャラの向きとマウスの向きを分離する）
            _cameraPivot = new GameObject("CameraTarget").transform;
            _cameraPivot.SetParent(_stageRoot.transform, false);
            _cameraYaw = 0f;
            _cameraPitch = 12f;
            UpdateCameraPivot();
            var rigRoot = new GameObject("CameraRigs").transform;
            rigRoot.SetParent(_stageRoot.transform, false);
            var brain = CameraRigBuilder.EnsureBrain(mainCamera, defaultBlendSeconds: 0.4f);

            var thirdPerson = CameraRigBuilder.CreateThirdPerson("Viewpoint_TPS", rigRoot,
                distance: 4.5f, shoulderSide: 0.5f, height: 1.2f);
            var firstPerson = CameraRigBuilder.CreateFirstPerson("Viewpoint_FPS", rigRoot,
                fieldOfView: 80f);
            var overhead = CameraRigBuilder.CreateFixed("Viewpoint_Overhead",
                new Vector3(0f, 13f, -9f), Vector3.zero, rigRoot);

            _cameraDirector = rigRoot.gameObject.AddComponent<CameraDirector>();
            _cameraDirector.Initialize(_hub, brain);
            _cameraDirector
                .Register(ViewpointId.ThirdPerson, thirdPerson)
                .Register(ViewpointId.FirstPerson, firstPerson, followsTarget: false)
                .Register(ViewpointId.Overhead, overhead, followsTarget: false);

            // 三人称は腰のダミーを追う（一括差し替えの対象はこちらだけ）
            _cameraDirector.SetTarget(_cameraPivot);

            // 一人称は頭に固定する（首の動き＝注視リグの結果がそのまま画面になる）
            var fpsTarget = firstPerson.Target;
            fpsTarget.TrackingTarget = _playerHead;
            firstPerson.Target = fpsTarget;

            _viewpointIndex = 0;
            _cameraDirector.SetBase(ViewpointId.ThirdPerson, blendSeconds: 0f);
        }

        /// <summary>
        /// エフェクトのプールを用意する（被弾のたびに Instantiate/Destroy しない）。
        /// 複製元は非表示のテンプレートで、プレハブ資産があればそれに差し替えるだけで済む。
        /// </summary>
        private void BuildPools()
        {
            var poolRoot = new GameObject("Pools").transform;
            poolRoot.SetParent(_stageRoot.transform, false);
            _pools = new PoolRegistry(poolRoot, defaultMaxRetained: 32);

            _hitEffectPrefab = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _hitEffectPrefab.name = "HitEffect";
            Object.Destroy(_hitEffectPrefab.GetComponent<Collider>()); // 通行の妨げにしない
            _hitEffectPrefab.transform.SetParent(poolRoot, false);
            _hitEffectPrefab.transform.localScale = Vector3.one * 0.45f;
            RendererTint.Set(_hitEffectPrefab.GetComponent<Renderer>(),
                new Color(1f, 0.85f, 0.25f));
            _hitEffectPrefab.SetActive(false);

            _pools.Prewarm(_hitEffectPrefab, 6); // 戦闘前に確保して実行中の生成を避ける
        }

        /// <summary>
        /// 原点回帰を用意する（広いフィールドでの座標精度の維持）。
        /// 閾値はデモ用に歩いて届く距離にしてある（実際のゲームでは 1000m 単位）。
        /// </summary>
        private void BuildOriginShift()
        {
            var host = new GameObject("OriginShift").transform;
            host.SetParent(_stageRoot.transform, false);
            _originShift = host.gameObject.AddComponent<OriginShiftSystem>();

            var focus = _playerController.Agent.ActiveActor.Avatar is Avatar3D avatar
                ? avatar.transform
                : null;
            _originShift.Initialize(_hub, focus, threshold: 45f);
            _originShift.AddRoot(_stageRoot.transform);  // 地形・カメラ・プールごと動かす
            _originShift.AddHandler(new Sample_OriginFollower(this));
        }

        /// <summary>
        /// 原点回帰への追従（App の方針実装）。
        /// Transform を持つものは根をずらせば済むが、純C#側に持っている座標——
        /// 演出座標（ActorPose）とトリガー位置——はここで自分で追従させる。
        /// </summary>
        private sealed class Sample_OriginFollower : IOriginShiftHandler
        {
            /// <summary>追従させる対象のフェーズ。</summary>
            private readonly Sample_BattlePhase _phase;

            /// <summary>Sample_OriginFollower を生成する。</summary>
            public Sample_OriginFollower(Sample_BattlePhase phase)
            {
                _phase = phase;
            }

            /// <summary>世界がずれたので自前の座標も合わせる。</summary>
            public void OnOriginShifted(Vector3 delta)
            {
                _phase.ShiftOwnCoordinates(delta);
            }
        }

        /// <summary>純C#側に持っている座標を原点移動へ追従させる。</summary>
        private void ShiftOwnCoordinates(Vector3 delta)
        {
            ShiftAgentPose(_playerId, delta);
            ShiftAgentPose(_enemyId, delta);
            for (var i = 0; i < _triggers.Count; i++)
            {
                _triggers[i].Position += delta;
            }
            _pickup?.ShiftOrigin(delta); // 拾う動作の目標点（純C#保持のワールド座標）
            // 足IKの時間追従は「前フレームからの差」で動くので、ワープ相当の移動では捨てる
            _playerFeet?.ResetFollow();
            Debug.Log($"[OriginShift] 世界を {delta} ずらした"
                + $"（累積 {_originShift.Shifter.TotalOffset}）");
        }

        /// <summary>1体ぶんの演出座標をずらす（表示にも即座に反映する）。</summary>
        private void ShiftAgentPose(CharacterId id, Vector3 delta)
        {
            if (!_characters.Registry.TryGet(id, out var agent))
            {
                return;
            }
            var pose = agent.ActiveActor.Pose;
            pose.Position += delta;
            agent.ActiveActor.Avatar.ApplyPose(pose.Position, pose.Rotation);
        }

        /// <summary>被弾位置にヒットエフェクトを出す（プールから借りる）。</summary>
        private void SpawnHitEffect(CharacterId target)
        {
            if (_pools == null || !_characters.Registry.TryGet(target, out var agent))
            {
                return;
            }
            var position = agent.ActiveActor.Pose.Position + Vector3.up * 0.9f;
            var effect = _pools.Rent(_hitEffectPrefab, position, Quaternion.identity);
            _liveEffects.Add((effect, 0.35f));
        }

        /// <summary>エフェクトの寿命を進め、切れたものをプールへ返す。</summary>
        private void UpdateEffects(float deltaTime)
        {
            for (var i = _liveEffects.Count - 1; i >= 0; i--)
            {
                var live = _liveEffects[i];
                var remain = live.Remain - deltaTime;
                if (remain <= 0f)
                {
                    _pools.Return(live.Instance);
                    _liveEffects.RemoveAt(i);
                    continue;
                }
                _liveEffects[i] = (live.Instance, remain);
            }
        }

        /// <summary>拾えるオーブを配置する（開けた場所・坂の脇・階段の上＝移動デモと連結）。</summary>
        private void BuildPickups()
        {
            _pickup = new Sample_PickupInteraction();
            _pickupClock = 0f;
            _pickup.PickedUp += (picked, total) =>
                Debug.Log($"[Pickup] アイテムを拾った（{picked}/{total}）");

            // 発光マテリアルは1枚だけ作って全オーブで共有する（暗黙の複製を避ける家風）
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null)
            {
                _pickupMaterial = new Material(shader) { name = "PickupOrb(Shared)" };
                _pickupMaterial.SetColor("_BaseColor", new Color(1f, 0.84f, 0.35f));
                _pickupMaterial.EnableKeyword("_EMISSION");
                _pickupMaterial.SetColor("_EmissionColor", new Color(1f, 0.72f, 0.2f) * 2.2f);
            }

            CreatePickupOrb(new Vector3(-2.5f, 0f, -1.5f));  // 開けた場所（最初に目へ入る）
            CreatePickupOrb(new Vector3(-3.2f, 0f, 2.5f));   // 坂の脇
            CreatePickupOrb(new Vector3(4.5f, 1.44f, 4.9f)); // 階段を登った台の上（移動→拾うの連結）
        }

        /// <summary>オーブを1個生成して拾える台帳へ登録する。</summary>
        private void CreatePickupOrb(Vector3 localPosition)
        {
            var root = new GameObject("PickupOrb");
            root.transform.SetParent(_stageRoot.transform, false);
            root.transform.localPosition = localPosition;

            var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            orb.name = "Orb";
            Object.Destroy(orb.GetComponent<Collider>()); // 見た目だけ＝移動・足IKのレイを邪魔しない
            orb.transform.SetParent(root.transform, false);
            orb.transform.localPosition = new Vector3(0f, PickupOrbHeight, 0f);
            orb.transform.localScale = Vector3.one * PickupOrbScale;
            var renderer = orb.GetComponent<Renderer>();
            if (_pickupMaterial != null)
            {
                renderer.sharedMaterial = _pickupMaterial;
            }
            else
            {
                RendererTint.Set(renderer, new Color(1f, 0.84f, 0.35f)); // URP 外の保険
            }

            // ほのかな点光源（「拾えるもの」の誘導。3個程度なら URP Forward で無視できる負荷）
            var glow = new GameObject("Glow", typeof(Light)).GetComponent<Light>();
            glow.transform.SetParent(orb.transform, false);
            glow.type = LightType.Point;
            glow.range = 2.4f;
            glow.intensity = 1.2f;
            glow.color = new Color(1f, 0.8f, 0.45f);

            _pickup.AddItem(orb.transform);
        }

        /// <summary>拾う動作の進行（状態はTick。腕IKへの反映は UpdateDemoRig=艶が担う）。</summary>
        private void TickPickup(float deltaTime)
        {
            if (_pickup == null || _playerController == null)
            {
                return;
            }
            var pose = _playerController.Agent.ActiveActor.Pose;
            _pickup.Tick(deltaTime, pose.Position, pose.PlanarSpeed, _playerHandBone);
        }

        /// <summary>
        /// オーブの浮遊・回転・接近パルス（拾える距離に入ると脈打って知らせる）。
        /// 時間基盤のdt（Drainが渡すゲームdt）で進める＝スロー・ヒットストップも世界と揃う。
        /// </summary>
        private void AnimatePickups(float deltaTime)
        {
            if (_pickup == null || _playerController == null)
            {
                return;
            }
            _pickupClock += deltaTime;
            var playerPosition = _playerController.Agent.ActiveActor.Pose.Position;
            var items = _pickup.Items;
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var bob = Mathf.Sin(_pickupClock * 2f + i * 1.7f) * 0.06f;
                var near = Vector3.Distance(item.position, playerPosition) < _pickup.StartRange;
                var pulse = near ? 1f + Mathf.Sin(_pickupClock * 8f) * 0.08f : 1f;
                item.localPosition = new Vector3(0f, PickupOrbHeight + bob, 0f);
                item.localScale = Vector3.one * (PickupOrbScale * pulse);
                item.Rotate(0f, 90f * deltaTime, 0f, Space.Self);
            }
        }

        /// <summary>
        /// 戦場の空気感を整える（フォグ＋光）。プリミティブ主体のステージでも
        /// 距離のフォグと柔らかい影があるだけで奥行きと接地感が出る。OnExit で戻す。
        /// </summary>
        private void ApplyAtmosphere()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 25f;
            RenderSettings.fogEndDistance = 80f;
            RenderSettings.fogColor = new Color(0.72f, 0.78f, 0.86f);

            var light = Object.FindFirstObjectByType<Light>();
            if (light != null && light.type == LightType.Directional)
            {
                light.shadows = LightShadows.Soft;      // 接地感の要
                light.intensity = 1.05f;
                light.color = new Color(1f, 0.96f, 0.9f); // わずかに暖色＝屋外光
                light.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            }
        }

        /// <summary>カメラ注視点をプレイヤーへ追従させ、マウスの向きを反映する。</summary>
        private void UpdateCameraPivot()
        {
            if (_cameraPivot == null || _playerController == null)
            {
                return;
            }
            var pose = _playerController.Agent.ActiveActor.Pose;
            var height = _playerModel != null ? _playerModel.Height * 0.72f : 1.1f;
            _cameraPivot.position = pose.Position + Vector3.up * height;
            _cameraPivot.rotation = Quaternion.Euler(_cameraPitch, _cameraYaw, 0f);
        }

        /// <summary>
        /// 移動入力が無いTickにも重力・着地を効かせる（Idle/Guard 中に宙に浮くのを防ぐ）。
        /// Locomotion が既にモーターを動かしたTickは何もしない。
        /// </summary>
        private void ApplyIdleGravity()
        {
            // プレイヤー・敵を問わず、モーター持ち全員に重力を効かせる
            // （敵が攻撃硬直・待機中に宙へ浮いたまま止まるのを防ぐ）
            for (var i = 0; i < _bodies.Count; i++)
            {
                var (agent, motor) = _bodies[i];
                if (motor.ConsumeMovedThisTick())
                {
                    continue;
                }
                var actor = agent.ActiveActor;
                actor.Pose.Position = motor.Move(actor.Pose.Position, Vector3.zero);
                actor.Avatar.ApplyPose(actor.Pose.Position, actor.Pose.Rotation);
            }
        }

        /// <summary>
        /// 速度・接地に応じてクリップと足IKの効きを選ぶ（歩き/走り/滞空の艶の方針）。
        /// Driver.Play は同じIDの再指定を流し直さないため、毎フレーム呼んで安全。
        /// </summary>
        private void UpdatePlayerAnimation()
        {
            if (_playerMotor == null || _playerController == null)
            {
                return;
            }
            var actor = _playerController.Agent.ActiveActor;
            var grounded = _playerMotor.IsGrounded;

            // 足IK: 立ち止まりは全効き・移動中は弱め・滞空は切る（足運びを邪魔しない）
            if (_playerFeet != null)
            {
                var target = !grounded ? 0f : actor.Pose.PlanarSpeed < 0.2f ? 1f : 0.2f;
                _playerFeet.Weight = Mathf.MoveTowards(
                    _playerFeet.Weight, target, Time.deltaTime * 5f);
            }

            var driver = _playerModel?.Avatar.Driver;
            if (driver == null)
            {
                return;
            }
            var key = actor.CurrentKey;
            if (!grounded)
            {
                // 滞空クリップを流すのは移動・待機のときだけ——攻撃・被弾・死亡の
                // モーションを上書きしない（滞空中に倒れると永久ジャンプポーズになる事故の防止）。
                // 非ループクリップは Play のたびに先頭から流し直される（Driver の
                // 再トリガー仕様＝攻撃連打用）ため、毎フレーム呼ぶと最初のポーズで凍る。
                // 再生中でないときだけ流す
                if ((key.Equals(BehaviorKey.Locomotion) || key.Equals(BehaviorKey.Idle))
                    && !driver.CurrentMotion.Equals(Sample_PlayerModel.JumpClipId))
                {
                    driver.Play(Sample_PlayerModel.JumpClipId, 0.12f);
                }
                return;
            }
            if (key.Equals(BehaviorKey.Locomotion))
            {
                // 歩き⇄走りはヒステリシス（境界1本だと閾値付近で毎フレーム往復して痙攣する）
                var speed = actor.Pose.PlanarSpeed;
                if (_runClipActive ? speed < 2.2f : speed > 3.2f)
                {
                    _runClipActive = !_runClipActive;
                }
                driver.Play(_runClipActive
                    ? MotionClipId.Locomotion
                    : Sample_PlayerModel.WalkClipId, 0.25f);
            }
            else if (key.Equals(BehaviorKey.Idle))
            {
                _runClipActive = false;
                // 討伐成功後の立ち止まりは勝利ポーズ（非ループ＝決めポーズで止まる）。
                // 非ループの Win は毎フレーム Play すると先頭で凍るため再生中は触らない
                var idleClip = _victory ? Sample_PlayerModel.WinClipId : MotionClipId.Idle;
                if (!driver.CurrentMotion.Equals(idleClip))
                {
                    driver.Play(idleClip, 0.25f);
                }
            }
        }

        /// <summary>子階層まで含めてレイヤーを付け替える（自己レイキャスト回避用）。</summary>
        private static void SetLayerRecursive(GameObject target, int layer)
        {
            target.layer = layer;
            var transform = target.transform;
            for (var i = 0; i < transform.childCount; i++)
            {
                SetLayerRecursive(transform.GetChild(i).gameObject, layer);
            }
        }

        /// <summary>
        /// リグの目標を毎フレーム更新する（頭は常に敵を注視、腕は近距離だけ伸びる）。
        /// 生死・距離による重みの決め方はゲームの演出方針＝ここ（App）が持つ。
        /// </summary>
        private void UpdateDemoRig()
        {
            UpdatePlayerAnimation();
            if (_playerLook == null)
            {
                return;
            }
            // まず「どこへ・どれだけ」の望みを決め、最後にまとめて平滑適用する。
            // 分岐（拾う⇄敵リーチ）の切替で目標・重みが1フレームで跳ねないための二段構え
            Vector3? lookDesired = null;
            var lookWeightDesired = 0f;
            Vector3? armDesired = null;
            var armWeightDesired = 0f;

            var actor = _playerController.Agent.ActiveActor;
            var speed = actor.Pose.PlanarSpeed;

            // 肘の曲げ方向の基準は「今の体の背中側」へ毎フレーム更新する。
            // 生成時の固定点のままだと移動・旋回・原点回帰で基準が置き去りになり、
            // 体の向きによって肘があらぬ方向へ曲がる
            _playerArm.SetPoleHint(actor.Pose.Position
                - actor.Pose.Rotation * Vector3.forward * 2f + Vector3.up);

            if (_pickup != null && _pickup.HasTarget)
            {
                // 拾う動作中は腕も視線もアイテムへ（敵への手伸ばしより優先）
                lookDesired = _pickup.TargetPoint;
                lookWeightDesired = 1f;
                armDesired = _pickup.TargetPoint;
                armWeightDesired = _pickup.Weight;
            }
            else if (_characters.Registry.TryGet(_enemyId, out var enemy) && enemy.IsAlive)
            {
                var enemyPosition = enemy.ActiveActor.Pose.Position;

                // 注視: 走行中は弱める（全力の首振りは走り姿勢を壊す）
                lookDesired = enemyPosition + Vector3.up * 0.5f;
                lookWeightDesired = speed > 0.5f ? 0.5f : 1f;

                // 腕IK: 立ち止まっているときだけ手を伸ばす（走行中の腕引っ張りは姿勢が崩れる）
                var distance = Vector3.Distance(actor.Pose.Position, enemyPosition);
                const float reachRange = 3f;
                if (distance < reachRange && speed < 0.8f
                    && (_playerMotor == null || _playerMotor.IsGrounded))
                {
                    armDesired = enemyPosition + Vector3.up * 0.6f;
                    armWeightDesired = 1f - distance / reachRange; // 近いほど強く伸びる
                }
            }

            ApplyAimSmoothing(lookDesired, lookWeightDesired, armDesired, armWeightDesired);
        }

        /// <summary>
        /// 注視・腕IKの目標と重みをなめらかに追従させて適用する（艶）。
        /// リグ側に平滑は無い（SetTarget 即時反映）ため、切替の吸収は配線側の責務。
        /// 目標が無くなったら重みを減衰させ、消え切ってから ClearTarget する。
        /// </summary>
        private void ApplyAimSmoothing(Vector3? lookDesired, float lookWeightDesired,
            Vector3? armDesired, float armWeightDesired)
        {
            var follow = 1f - Mathf.Exp(-14f * Time.deltaTime); // 目標位置の追従率
            var weightStep = Time.deltaTime * 6f;               // 重みの最大変化速度

            if (lookDesired.HasValue)
            {
                _lookPoint = _lookPointValid
                    ? Vector3.Lerp(_lookPoint, lookDesired.Value, follow)
                    : lookDesired.Value;
                _lookPointValid = true;
                _lookWeightBlend = Mathf.MoveTowards(_lookWeightBlend, lookWeightDesired, weightStep);
                _playerLook.Weight = _lookWeightBlend;
                _playerLook.SetTarget(_lookPoint);
            }
            else
            {
                _lookWeightBlend = Mathf.MoveTowards(_lookWeightBlend, 0f, weightStep);
                if (_lookWeightBlend <= 0.001f)
                {
                    _playerLook.ClearTarget();
                    _lookPointValid = false;
                }
                else
                {
                    _playerLook.Weight = _lookWeightBlend;
                }
            }

            if (armDesired.HasValue)
            {
                _armPoint = _armPointValid
                    ? Vector3.Lerp(_armPoint, armDesired.Value, follow)
                    : armDesired.Value;
                _armPointValid = true;
                _armWeightBlend = Mathf.MoveTowards(_armWeightBlend, armWeightDesired, weightStep);
                _playerArm.Weight = _armWeightBlend;
                _playerArm.SetTarget(_armPoint);
            }
            else
            {
                _armWeightBlend = Mathf.MoveTowards(_armWeightBlend, 0f, weightStep);
                if (_armWeightBlend <= 0.001f)
                {
                    _playerArm.ClearTarget();
                    _armPointValid = false;
                }
                else
                {
                    _playerArm.Weight = _armWeightBlend;
                }
            }
        }

        /// <summary>行動開始の出来事をHubメッセージへ変換する（攻撃のみ関心）。</summary>
        private void OnBehaviorStarted(CharacterBehaviorEvent ev)
        {
            if (!ev.Behavior.Equals(BehaviorKey.Attack))
            {
                return;
            }
            var target = ev.Id.Equals(_playerId) ? _enemyId : _playerId;
            // 対象のガード状態は Behavior の真実から埋める（入力の写しではない＝
            // のけぞり中・攻撃中は構えが解けているという実状態がそのまま伝わる）
            var guarding = _characters.Registry.TryGet(target, out var targetAgent)
                && targetAgent.ActiveActor.CurrentKey.Equals(BehaviorKey.Guard);
            _hub.Publish(new AttackRequested(ev.Id, target, ev.Payload, partId: 0, targetGuarding: guarding));
            _director.NotifyAttackStarted(ev.Id); // 攻撃権の消費（タイマーが戻る）
        }

        /// <summary>3Dモデルの Avatar を組む（舞台ルートの下）。</summary>
        private Avatar3D CreateModelAvatar(string name, PrimitiveType primitive, Color color)
        {
            var root = new GameObject(name);
            root.transform.SetParent(_stageRoot.transform, false);

            var visual = GameObject.CreatePrimitive(primitive);
            visual.name = "Visual";
            visual.transform.SetParent(root.transform, false);
            RendererTint.Set(visual.GetComponent<Renderer>(), color);

            var avatar = root.AddComponent<Avatar3D>();
            avatar.Configure(animator: null, visualRoot: visual.transform);
            return avatar;
        }

        /// <summary>2D立ち絵の Avatar を組む（舞台ルートの下）。</summary>
        private Avatar2D CreatePortraitAvatar(string name, Color color)
        {
            var root = new GameObject(name);
            root.transform.SetParent(_stageRoot.transform, false);

            var spriteObject = new GameObject("Portrait");
            spriteObject.transform.SetParent(root.transform, false);
            var renderer = spriteObject.AddComponent<SpriteRenderer>();
            renderer.sprite = CreateSolidSprite(color);

            var avatar = root.AddComponent<Avatar2D>();
            avatar.Configure(renderer, sprites: null, billboardToCamera: true);
            return avatar;
        }

        /// <summary>単色の立ち絵スプライトを生成する。</summary>
        private static Sprite CreateSolidSprite(Color color)
        {
            const int width = 32;
            const int height = 64;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color[width * height];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, width, height),
                new Vector2(0.5f, 0.5f), 32f);
        }

        /// <summary>画面2枚を生成して登録する（舞台ルートの下＝フェーズ退場で消える）。</summary>
        private void BuildScreens(UISystem uiSystem, Sample_StageSpec stage)
        {
            var hud = new GameObject("Sample_HudScreen").AddComponent<Sample_HudScreen>();
            hud.transform.SetParent(_stageRoot.transform, false);
            hud.Construct(_playerId, NameOf);
            hud.SetInitialLine(_playerId, _world.Hunter.Hp, _world.Hunter.MaxHp);
            hud.SetInitialLine(_enemyId, _world.Monster.Hp, _world.Monster.MaxHp);
            uiSystem.Register(hud);

            _resultScreen = new GameObject("Sample_ResultScreen").AddComponent<Sample_ResultScreen>();
            _resultScreen.transform.SetParent(_stageRoot.transform, false);
            uiSystem.Register(_resultScreen);
        }

        /// <summary>CharacterId→表示名の解決（HUDへ差し込む）。</summary>
        private string NameOf(CharacterId id)
        {
            return id.Equals(_playerId) ? _world.Hunter.Name : _world.Monster.Name;
        }
    }
}
