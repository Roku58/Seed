// ============================================================================
// 【サンプルコード】Sample_ActionBattle3DRunner
// 3D空間＋物理の当たり判定で遊ぶアクションバトルの統合デモ。
//
// 空のGameObjectにアタッチしてPlayするだけで、地面・カメラ・ライト・
// プレイヤー（青カプセル）・モンスター（灰ボディ＋頭・翼の部位コライダー）を
// すべてコードで生成する。
//
// 含まれる実装パターン:
//   3D当たり判定       … Sample_HitboxTrigger3D ＋ Sample_PartMarker3D（頭/翼で肉質が変わる）
//   入力による状態変化 … Sample_PlayerController3D（Idle/Attacking/Guarding/Dead）
//   エネミーAI         … Sample_MonsterController3D（Cooldown→予兆→攻撃）
//   Animator反映       … Sample_SimpleCharacterView（割り当てればSetTrigger、無くても簡易演出）
//   レコード→画面反映 … 本クラス（RecordPresenterBase）が被弾演出とログへ翻訳
//
// 操作: WASD移動 / [1]斬り上げ / [3]溜め斬り / [4]鬼人薬 / [G]ガード / [R]決着後リスタート
// ※ 旧Inputクラスを使うため Active Input Handling は「Input Manager (Old)」か「Both」。
// ============================================================================

using System.Collections.Generic;
using Seed.Core.Presenter;
using Seed.Core.Samples.ActionBattle;
using UnityEngine;

namespace Seed.Core.Samples
{
    /// <summary>【サンプル】3D当たり判定・状態機械・敵AI・Animator反映の統合ランナー。</summary>
    public sealed class Sample_ActionBattle3DRunner : RecordPresenterBase<Sample_Record>
    {
        /// <summary>ロジック乱数の基準シード（リスタートごとに+1）。</summary>
        [SerializeField] private uint _logicSeed = 900;

        /// <summary>現在の狩りの世界。</summary>
        private Sample_ActionWorld _world;

        /// <summary>ID解決用のエンティティ台帳（表示に使う）。</summary>
        private EntityRegistry _registry;

        /// <summary>プレイヤー制御。</summary>
        private Sample_PlayerController3D _player;

        /// <summary>モンスターAI。</summary>
        private Sample_MonsterController3D _monster;

        /// <summary>プレイヤーの見た目。</summary>
        private Sample_SimpleCharacterView _playerView;

        /// <summary>モンスターの見た目。</summary>
        private Sample_SimpleCharacterView _monsterView;

        /// <summary>画面に出す直近ログ。</summary>
        private readonly List<string> _lines = new List<string>(12);

        /// <summary>決着済みか。</summary>
        private bool _isOver;

        /// <summary>リスタート回数（シードへ加算）。</summary>
        private uint _battleIndex;

        /// <summary>ロジック時間の端数繰り越し（float秒→ms の境界変換）。</summary>
        private LogicTimeAccumulator _logicTime;

        /// <summary>起動時に舞台と世界を組み立てる。</summary>
        private void Start()
        {
            BuildStage();
            StartHunt();
        }

        /// <summary>毎フレーム、ロジック時間を進めて決着を監視する（各制御は自身のUpdateで動く）。</summary>
        private void Update()
        {
            if (_isOver)
            {
                if (Input.GetKeyDown(KeyCode.R))
                {
                    _battleIndex++;
                    StartHunt();
                }
                return;
            }

            AdvanceLogicTime();
            CheckBattleEnd();
        }

        // ================================================================
        // 進行
        // ================================================================

        /// <summary>世界を作り直し、各制御へ配線する。</summary>
        private void StartHunt()
        {
            var seed = _logicSeed + _battleIndex;
            _world = new Sample_ActionWorld(seed, trace: null);
            _registry = _world.Registry;
            _lines.Clear();
            _logicTime.Reset();
            _isOver = false;

            FixupPartMarkers();
            _playerView.ResetView();
            _monsterView.ResetView();
            _player.Initialize(_world, _player.GetComponentInChildren<Sample_HitboxTrigger3D>(), _playerView);
            _monster.Initialize(_world, _player, _monsterView, PushLine);

            Attach(Sample_ActionContext.Log(_world.Ctx), consumeExisting: true);
            PushLine($"――― 狩り開始（シード {seed}）。WASD移動 [1][3]攻撃 [4]鬼人薬 [G]ガード ―――");
        }

        /// <summary>フレームの経過時間をロジック時間へ変換して進める（スタミナ回復・効果切れ）。</summary>
        private void AdvanceLogicTime()
        {
            var ms = _logicTime.Advance(Time.deltaTime);
            if (ms <= 0)
            {
                return;
            }
            _world.Driver.AdvanceTimeMs(ms);
        }

        /// <summary>どちらかが倒れたら決着にする（倒れ演出は各コントローラーが行う）。</summary>
        private void CheckBattleEnd()
        {
            if (!_world.Hunter.IsDead && !_world.Monster.IsDead)
            {
                return;
            }
            _isOver = true;
            var result = _world.Monster.IsDead ? "討伐成功！" : "力尽きた…";
            PushLine($"――― {result} [R]でリスタート ―――");
        }

        // ================================================================
        // レコード → 画面反映
        // ================================================================

        /// <summary>レコードを被弾演出とログへ翻訳する（★レコード→Viewの対応表）。</summary>
        protected override void Dispatch(in Sample_Record record)
        {
            PushLine(Sample_ActionBattlePresenter.Format(record, _registry));

            if (record.Kind == Sample_RecordKind.HitDamage && record.Value > 0)
            {
                ViewOfUnitId(record.TargetId)?.PlayHit();
            }
            if (record.Kind == Sample_RecordKind.StatusTriggered && record.Status == Sample_StatusKind.Blast)
            {
                _monsterView.PlayHit(); // 爆破は必ずモンスター側
            }
        }

        /// <summary>ユニットID→ビューの対応（ID方式レコードなので台帳のIDで比較する）。</summary>
        private Sample_SimpleCharacterView ViewOfUnitId(int unitId)
        {
            if (unitId == _registry.GetId(_world.Hunter))
            {
                return _playerView;
            }
            if (unitId == _registry.GetId(_world.Monster))
            {
                return _monsterView;
            }
            return null;
        }

        /// <summary>ログ行を追加する（画面には直近10行、Consoleには全行）。</summary>
        private void PushLine(string line)
        {
            _lines.Add(line);
            if (_lines.Count > 10)
            {
                _lines.RemoveAt(0);
            }
            Debug.Log(line);
        }

        // ================================================================
        // 舞台の生成
        // ================================================================

        /// <summary>地面・カメラ・ライト・プレイヤー・モンスター（部位コライダー付き）を生成する。</summary>
        private void BuildStage()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(2f, 1f, 2f);

            EnsureCameraAndLight();
            BuildPlayer();
            BuildMonster();
        }

        /// <summary>プレイヤー（カプセル＋前方ヒットボックス）を生成する。</summary>
        private void BuildPlayer()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Player";
            go.transform.position = new Vector3(0f, 1f, -4f);
            go.GetComponent<Renderer>().material.color = new Color(0.25f, 0.45f, 0.9f);

            // トリガー接触に必要なRigidbody（物理で飛ばないようkinematic）
            var body = go.AddComponent<Rigidbody>();
            body.isKinematic = true;

            // 前方のヒットボックス（普段は無効。攻撃の有効フレームだけ有効化される）
            var hitbox = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            hitbox.name = "AttackHitbox";
            hitbox.transform.SetParent(go.transform, false);
            hitbox.transform.localPosition = new Vector3(0f, 0f, 1.2f);
            hitbox.transform.localScale = Vector3.one * 1.2f;
            hitbox.GetComponent<Renderer>().enabled = false; // 判定は見せない
            hitbox.GetComponent<Collider>().isTrigger = true;
            hitbox.AddComponent<Sample_HitboxTrigger3D>();

            _playerView = go.AddComponent<Sample_SimpleCharacterView>();
            _playerView.SetFacing(Vector3.forward);
            _player = go.AddComponent<Sample_PlayerController3D>();
        }

        /// <summary>モンスター（ボディ＋頭・翼の部位コライダー）を生成する。</summary>
        private void BuildMonster()
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Monster";
            body.transform.position = new Vector3(0f, 1.5f, 2f);
            body.transform.localScale = new Vector3(2f, 3f, 2f);
            body.GetComponent<Renderer>().material.color = new Color(0.45f, 0.45f, 0.5f);

            CreatePart(body.transform, "Head", PrimitiveType.Sphere,
                new Vector3(0f, 0.65f, -0.55f), Vector3.one * 0.5f,
                new Color(0.8f, 0.3f, 0.3f), isHead: true);
            CreatePart(body.transform, "Wing", PrimitiveType.Cube,
                new Vector3(0.75f, 0.2f, 0f), new Vector3(0.25f, 0.5f, 0.9f),
                new Color(0.6f, 0.6f, 0.7f), isHead: false);

            _monsterView = body.AddComponent<Sample_SimpleCharacterView>();
            _monsterView.SetFacing(Vector3.back);
            _monster = body.AddComponent<Sample_MonsterController3D>();
        }

        /// <summary>部位コライダー（マーカー付き）を生成する。肉質の違いは色でも示す。</summary>
        private void CreatePart(Transform parent, string name, PrimitiveType primitive,
            Vector3 localPosition, Vector3 localScale, Color color, bool isHead)
        {
            var go = GameObject.CreatePrimitive(primitive);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().material.color = color;

            var marker = go.AddComponent<Sample_PartMarker3D>();
            // 舞台は世界より先に作るため、ロジック側の部位は StartHunt 時に
            // FixupPartMarkers で差し込む（リスタートで新しい世界に差し替わる）
            _pendingMarkers.Add((marker, isHead));
        }

        /// <summary>部位マーカーの遅延初期化用バッファ。</summary>
        private readonly List<(Sample_PartMarker3D marker, bool isHead)> _pendingMarkers =
            new List<(Sample_PartMarker3D, bool)>();

        /// <summary>世界生成後に、部位マーカーへロジック側の部位を差し込む。</summary>
        private void FixupPartMarkers()
        {
            for (var i = 0; i < _pendingMarkers.Count; i++)
            {
                var (marker, isHead) = _pendingMarkers[i];
                marker.Initialize(isHead ? _world.Head : _world.Wing);
            }
        }

        /// <summary>メインカメラとライトを（無ければ作って）配置する。</summary>
        private static void EnsureCameraAndLight()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                camera = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
                camera.tag = "MainCamera";
            }
            camera.transform.position = new Vector3(0f, 9f, -9f);
            camera.transform.LookAt(new Vector3(0f, 1f, 0f));

            if (Object.FindFirstObjectByType<Light>() == null)
            {
                var light = new GameObject("Directional Light", typeof(Light)).GetComponent<Light>();
                light.type = LightType.Directional;
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }
        }

        /// <summary>HP・スタミナ・状態・ログを描画する。</summary>
        private void OnGUI()
        {
            if (_world == null)
            {
                return;
            }
            var hunter = _world.Hunter;
            var monster = _world.Monster;
            GUI.Label(new Rect(16f, 8f, 1200f, 24f),
                $"{hunter.Name} HP {hunter.Hp}/{hunter.MaxHp}  スタミナ {hunter.Stamina}/{hunter.MaxStamina}" +
                $"  状態:{_player.Current}    {monster.Name} HP {monster.Hp}/{monster.MaxHp}  AI:{_monster.Current}");

            var y = 34f;
            for (var i = 0; i < _lines.Count; i++)
            {
                GUI.Label(new Rect(16f, y, 1400f, 22f), _lines[i]);
                y += 20f;
            }
        }
    }
}
