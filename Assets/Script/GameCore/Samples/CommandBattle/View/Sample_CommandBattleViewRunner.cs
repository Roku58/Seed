// ============================================================================
// 【サンプルコード】Sample_CommandBattleViewRunner
// コマンドバトルの結果を「画面上のキャラクター」へ反映するデモ。
//
// カプセル2体（リザードン=赤 / ピカチュウ=黄）をコードで生成し、
// レコード列を RecordPlaybackQueue で1件ずつ再生しながら
// Sample_SimpleCharacterView（★Animator反映つき）へ翻訳する。
//   ActionDeclared → 攻撃の踏み込み / Hit → のけぞり＋赤フラッシュ
//   Guarding       → ガード表示     / 残りHP0 → 倒れる
//
// Animator を反映したい場合は、生成されたカプセルの
// Sample_SimpleCharacterView に AnimatorController 付きの Animator を割り当てるだけでよい
// （パラメータ名: Attack / Hit / Faint / Guarding）。
//
// シナリオは自動進行（3ターンの筋書き）。操作して遊ぶ版は
// Sample_CommandBattleInteractiveRunner を参照。
// ============================================================================

using Seed.Core.Presenter;
using Seed.Core.Samples.CommandBattle;
using UnityEngine;

namespace Seed.Core.Samples
{
    /// <summary>【サンプル】レコード→キャラビュー（Animator対応）反映のコマンドバトルデモ。</summary>
    public sealed class Sample_CommandBattleViewRunner : MonoBehaviour
    {
        /// <summary>ロジック乱数のシード。</summary>
        [SerializeField] private uint _logicSeed = 11;

        /// <summary>1レコードあたりの再生間隔（秒）。</summary>
        [SerializeField] private float _playbackInterval = 0.6f;

        /// <summary>バトル世界（合成ルートは Sample_CommandWorld を再利用）。</summary>
        private Sample_CommandWorld _world;

        /// <summary>レコードの順次再生キュー。</summary>
        private RecordPlaybackQueue<Sample_Record> _queue;

        /// <summary>レコードログの消費カーソル。</summary>
        private int _cursor;

        /// <summary>リザードン側のビュー。</summary>
        private Sample_SimpleCharacterView _charizardView;

        /// <summary>ピカチュウ側のビュー。</summary>
        private Sample_SimpleCharacterView _pikachuView;

        /// <summary>画面下部に出す直近ログ。</summary>
        private string _lastLine = "";

        /// <summary>シーンとバトルを組み立て、自動シナリオを解決して再生を始める。</summary>
        private void Start()
        {
            BuildStage();

            _world = new Sample_CommandWorld(_logicSeed, trace: null);
            _queue = new RecordPlaybackQueue<Sample_Record>(CreateStep);

            PlayScriptedTurns();
            _queue.EnqueueFrom(Sample_CommandContext.Log(_world.Ctx), ref _cursor);
        }

        /// <summary>毎フレーム、再生キューを進める。</summary>
        private void Update()
        {
            _queue.Tick(Time.deltaTime);
        }

        /// <summary>
        /// 自動シナリオ（3ターン）。結果はここで一瞬で確定し、あとは演出が追いかける。
        /// T1: 撃ち合い（やけど・せいでんき割り込み・きのみ連鎖が出るシード）
        /// T2: ピカチュウが まもる → リザードンの攻撃は失敗
        /// T3: リザードンが押し切る
        /// </summary>
        private void PlayScriptedTurns()
        {
            _world.Driver.RunTurn(
                new Sample_TurnAction(_world.Charizard, _world.Pikachu, _world.FirePunch),
                new Sample_TurnAction(_world.Pikachu, _world.Charizard, _world.Thunderbolt));
            _world.Driver.RunTurn(
                new Sample_TurnAction(_world.Pikachu, _world.Charizard, _world.Protect),
                new Sample_TurnAction(_world.Charizard, _world.Pikachu, _world.FirePunch));
            _world.Driver.RunTurn(
                new Sample_TurnAction(_world.Charizard, _world.Pikachu, _world.FirePunch),
                new Sample_TurnAction(_world.Pikachu, _world.Charizard, _world.Thunderbolt));
        }

        // ================================================================
        // レコード → ビューへの翻訳
        // ================================================================

        /// <summary>レコード1件を「ビュー反映＋一定時間待つ」ステップへ変換する。</summary>
        private IPlaybackStep CreateStep(Sample_Record record)
        {
            var captured = record; // クロージャへコピーを固定
            return new TimedStep(_playbackInterval, onStart: () => ApplyRecord(captured));
        }

        /// <summary>レコード種別ごとにビューへ反映する（★レコード→Animator/演出の対応表）。</summary>
        private void ApplyRecord(Sample_Record record)
        {
            _lastLine = Sample_CommandBattlePresenter.Format(record);

            switch (record.Kind)
            {
                case Sample_RecordKind.ActionDeclared:
                    ViewOf(record.Actor).PlayAttack();
                    break;

                case Sample_RecordKind.Hit:
                    ViewOf(record.Target).PlayHit();
                    if (record.Value2 <= 0)
                    {
                        ViewOf(record.Target).PlayFaint(); // HPスナップショットが0なら倒れる
                    }
                    break;

                case Sample_RecordKind.Guarding:
                    ViewOf(record.Actor).SetGuarding(true);
                    break;

                case Sample_RecordKind.TurnEnd:
                    _charizardView.SetGuarding(false);
                    _pikachuView.SetGuarding(false);
                    break;
            }
        }

        /// <summary>アクター参照→ビューの対応（参照方式レコードなので直接比較できる）。</summary>
        private Sample_SimpleCharacterView ViewOf(Sample_Actor actor)
        {
            return actor == _world.Charizard ? _charizardView : _pikachuView;
        }

        // ================================================================
        // 舞台の生成
        // ================================================================

        /// <summary>地面・カメラ・カプセル2体を生成する。</summary>
        private void BuildStage()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";

            EnsureCameraAndLight(new Vector3(0f, 4f, -7f), new Vector3(0f, 1f, 0f));

            _charizardView = CreateActor("Charizard", new Vector3(-2.5f, 1f, 0f), new Color(0.9f, 0.35f, 0.2f), Vector3.right);
            _pikachuView = CreateActor("Pikachu", new Vector3(2.5f, 1f, 0f), new Color(0.95f, 0.85f, 0.2f), Vector3.left);
        }

        /// <summary>カプセル＋ビューでアクターの見た目を作る。</summary>
        private static Sample_SimpleCharacterView CreateActor(string name, Vector3 position, Color color, Vector3 facing)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = name;
            go.transform.position = position;
            go.GetComponent<Renderer>().material.color = color;

            var view = go.AddComponent<Sample_SimpleCharacterView>();
            view.SetFacing(facing);
            return view;
        }

        /// <summary>メインカメラとライトを（無ければ作って）配置する。</summary>
        private static void EnsureCameraAndLight(Vector3 cameraPosition, Vector3 lookAt)
        {
            var camera = Camera.main;
            if (camera == null)
            {
                camera = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
                camera.tag = "MainCamera";
            }
            camera.transform.position = cameraPosition;
            camera.transform.LookAt(lookAt);

            if (Object.FindFirstObjectByType<Light>() == null)
            {
                var light = new GameObject("Directional Light", typeof(Light)).GetComponent<Light>();
                light.type = LightType.Directional;
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }
        }

        /// <summary>HPと直近ログを表示する。</summary>
        private void OnGUI()
        {
            if (_world == null)
            {
                return;
            }
            var c = _world.Charizard;
            var p = _world.Pikachu;
            GUI.Label(new Rect(16f, 8f, 900f, 24f),
                $"{c.Name} HP {c.Hp}/{c.MaxHp}    {p.Name} HP {p.Hp}/{p.MaxHp}" +
                (_queue.IsIdle ? "    （再生完了）" : string.Empty));
            GUI.Label(new Rect(16f, 32f, 1200f, 24f), _lastLine);
        }
    }
}
