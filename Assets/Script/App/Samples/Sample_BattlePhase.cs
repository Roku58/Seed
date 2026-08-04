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
using Seed.Motion;
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
            BuildScreens(uiSystem, stage);
            _hub.PublishCommand(new ShowScreenCommand(Sample_ScreenIds.BattleHud));

            // 6. 1フレームの実行順（メタAIの采配 → ユニットの思考、の順を登録順で保証）
            _pipeline = new TickPipeline();
            _pipeline.Add(TickPhase.Input, HandlePlayerInput);
            _pipeline.Add(TickPhase.Simulation, dt => _director.Tick(dt));
            _pipeline.Add(TickPhase.Simulation, dt => _characters.Tick(dt));
            _pipeline.Add(TickPhase.LogicTime, AdvanceLogicTime);
            _pipeline.Add(TickPhase.Drain, _ => _bridge.Drain());
            _pipeline.Add(TickPhase.Drain, _ => CheckBattleEnd());
            _pipeline.Add(TickPhase.Drain, _ => CheckTriggers());
            _pipeline.Add(TickPhase.Drain, _ => UpdateDemoRig());
            _pipeline.Add(TickPhase.Drain, _ => uiSystem.Tick(_clock.UnscaledDelta));

            // 被弾の瞬間だけ世界を止める（ヒットストップ）——時間基盤への命令1発で全基盤に効く
            var battleSubscriptions = _scope.Own(new SubscriptionBag(1));
            _hub.Subscribe<CharacterDamaged>(message =>
            {
                _hub.PublishCommand(new HitStopCommand(0.06f));
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
            _triggers.Clear();
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

            // [O] 自動操縦の切替（プレイヤーとAIの制御共通化のデモ）
            if (_input.WasPressedThisFrame(ActionId.Jump))
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

            var move = _input.Move;
            manual.SetMove(new Vector3(move.x, 0f, move.y));

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
            var result = _world.Monster.IsDead ? "討伐成功！" : "力尽きた…";
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

            BuildPlayerUnit(_catalog.Get<Sample_UnitSpec>(_playerId.Value), new Vector3(0f, 1f, -3f),
                attachBody: false);
            BuildEnemyUnit(_catalog.Get<Sample_UnitSpec>(_enemyId.Value), stage, new Vector3(0f, 1.5f, 2f),
                attachBody: false);
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
            var agent = CharacterFactory.Create(_playerId, spec.ToDefinition(),
                new ActorBlueprint(ModelActor,
                    CreateModelAvatar("Player", PrimitiveType.Capsule, new Color(0.25f, 0.45f, 0.9f))),
                new ActorBlueprint(PortraitActor,
                    CreatePortraitAvatar("Player2D", new Color(0.35f, 0.55f, 1f))));
            SetSpawnPose(agent, spawn, Quaternion.identity);
            if (attachBody)
            {
                AttachBody(agent);
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
        /// 生成ステージ用の当たりを装着する（CharacterController＋MotionSolver）。
        /// これにより生成した壁・建物が実際に移動を遮る（IMotionSolver の実演）。
        /// 2D立ち絵Actorへ切り替えている間はコントローラが非アクティブになり、
        /// Solver は素通しに落ちる（CharacterControllerMotionSolver の仕様）。
        /// </summary>
        private static void AttachBody(CharacterAgent agent)
        {
            if (!(agent.ActiveActor.Avatar is Avatar3D avatar))
            {
                return;
            }
            var controller = avatar.gameObject.AddComponent<CharacterController>();
            controller.height = 1.6f;
            controller.radius = 0.35f;
            controller.center = Vector3.zero;
            agent.ActiveActor.MotionSolver = new CharacterControllerMotionSolver(controller);
        }

        /// <summary>
        /// 姿勢・IKのデモリグを装着する（Seed.Motion の実演。モデルアセット無しでも動く）。
        /// 頭キューブ=注視（敵を目で追う姿勢制御）/ 左腕のプリミティブ関節=2ボーンIK /
        /// 後頭部のポニーテール=揺れもの（SpringBoneRig）。
        /// アニメの上へ LateUpdate で重なる「艶」であり、真実（Pose・行動）には触れない。
        /// </summary>
        private void AttachDemoMotionRig(CharacterAgent agent)
        {
            if (!(agent.ActiveActor.Avatar is Avatar3D avatar))
            {
                return;
            }
            var root = avatar.transform;

            // 頭（注視デモ用の小キューブ。向きが分かるよう鼻をつける）
            var head = new GameObject("Head").transform;
            head.SetParent(root, false);
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
            var shoulder = CreateJoint(root, "Shoulder", new Vector3(0.45f, 0.7f, 0f));
            var elbow = CreateJoint(shoulder, "Elbow", new Vector3(0f, -0.35f, 0f));
            var hand = CreateJoint(elbow, "Hand", new Vector3(0f, -0.35f, 0f));

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

            // リグ: 注視（頭のみ・最大70度）＋ 腕IK（肘は背中側へ曲がる）＋ 揺れもの
            _playerLook = new LookAtRig((head, 1f, 70f));
            _playerArm = new TwoBoneIkRig(shoulder, elbow, hand,
                poleHint: root.position - root.forward * 2f + Vector3.up);
            avatar.gameObject.AddComponent<MotionRig>()
                .With(_playerLook)   // 1) 頭の向き
                .With(_playerArm)    // 2) 腕IK
                .With(tail);         // 3) 揺れは最後（注視で動いた頭に追従＝順序が意味を持つ）
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
        /// リグの目標を毎フレーム更新する（頭は常に敵を注視、腕は近距離だけ伸びる）。
        /// 生死・距離による重みの決め方はゲームの演出方針＝ここ（App）が持つ。
        /// </summary>
        private void UpdateDemoRig()
        {
            if (_playerLook == null)
            {
                return;
            }
            if (!_characters.Registry.TryGet(_enemyId, out var enemy) || !enemy.IsAlive)
            {
                _playerLook.ClearTarget();
                _playerArm.ClearTarget();
                return;
            }

            var enemyPosition = enemy.ActiveActor.Pose.Position;
            _playerLook.SetTarget(enemyPosition + Vector3.up * 0.5f);

            var playerPosition = _playerController.Agent.ActiveActor.Pose.Position;
            var distance = Vector3.Distance(playerPosition, enemyPosition);
            const float reachRange = 3f;
            if (distance < reachRange)
            {
                _playerArm.Weight = 1f - distance / reachRange; // 近いほど強く伸びる
                _playerArm.SetTarget(enemyPosition + Vector3.up * 0.6f);
            }
            else
            {
                _playerArm.ClearTarget();
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
