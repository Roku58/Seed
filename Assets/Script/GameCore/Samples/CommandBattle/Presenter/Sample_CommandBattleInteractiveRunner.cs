// ============================================================================
// 【サンプルコード】Sample_CommandBattleInteractiveRunner
// ユーザー入力（uGUI の Button）で進行する対話型のコマンドバトル。
//
// プレイヤーはリザードンを操作し、毎ターン「ほのおのパンチ」か「まもる」を選ぶ。
// 敵ピカチュウは簡単なAI（HPが減ると一度だけ まもる、それ以外は 10まんボルト）。
//
// 実演している基盤パターン:
// - 入力待ち → 解決（RunTurn は一瞬）→ 順次再生（RecordPlaybackQueue）→ 入力待ち の状態機械
// - 再生中は Button を無効化し、結果が確定してから演出だけが時間をかけて流れる
// - UIはすべてコードで生成するため、空の GameObject にアタッチするだけで動く
// ============================================================================

using Seed.Core.Presenter;
using Seed.Core.Samples.CommandBattle;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Seed.Core.Samples
{
    /// <summary>【サンプル】Button操作で遊ぶ対話型コマンドバトルのランナー。</summary>
    public sealed class Sample_CommandBattleInteractiveRunner : MonoBehaviour
    {
        /// <summary>対話フェーズの状態。</summary>
        private enum Phase
        {
            /// <summary>プレイヤーの入力待ち（Buttonが有効）。</summary>
            WaitingInput,
            /// <summary>ターン結果を順次再生中（Buttonは無効）。</summary>
            Playing,
            /// <summary>決着済み（再戦Buttonのみ有効）。</summary>
            Finished,
        }

        /// <summary>ロジック乱数の基準シード（再戦ごとに+1して展開を変える）。</summary>
        [SerializeField] private uint _logicSeed = 100;

        /// <summary>1レコードあたりの表示間隔（秒）。演出側の都合であり結果には影響しない。</summary>
        [SerializeField] private float _playbackInterval = 0.35f;

        /// <summary>現在のバトル世界（合成ルートは Sample_CommandWorld を再利用）。</summary>
        private Sample_CommandWorld _world;

        /// <summary>レコード→ログ表示の順次再生キュー。</summary>
        private RecordPlaybackQueue<Sample_Record> _queue;

        /// <summary>レコードログの消費カーソル。</summary>
        private int _cursor;

        /// <summary>現在のフェーズ。</summary>
        private Phase _phase;

        /// <summary>再戦回数（シードへ加算して展開を変える）。</summary>
        private uint _battleIndex;

        /// <summary>敵AIが「まもる」を使用済みか（1試合に1回だけ使う）。</summary>
        private bool _enemyProtected;

        /// <summary>HP表示ラベル。</summary>
        private Text _hpText;

        /// <summary>戦闘ログ表示エリア。</summary>
        private Text _logText;

        /// <summary>「ほのおのパンチ」ボタン。</summary>
        private Button _attackButton;

        /// <summary>「まもる」ボタン。</summary>
        private Button _protectButton;

        /// <summary>「再戦」ボタン。</summary>
        private Button _restartButton;

        /// <summary>起動時にUIを組み立て、最初のバトルを開始する。</summary>
        private void Start()
        {
            BuildUi();
            StartBattle();
        }

        /// <summary>毎フレーム、再生キューを進めてフェーズを更新する。</summary>
        private void Update()
        {
            if (_queue == null)
            {
                return;
            }

            _queue.Tick(Time.deltaTime);

            if (_phase == Phase.Playing && _queue.IsIdle)
            {
                OnPlaybackFinished();
            }
        }

        // ================================================================
        // バトル進行
        // ================================================================

        /// <summary>新しい世界を組み立ててバトルを開始する。</summary>
        private void StartBattle()
        {
            _world = new Sample_CommandWorld(_logicSeed + _battleIndex, trace: null);
            _queue = new RecordPlaybackQueue<Sample_Record>(CreateStep);
            _cursor = 0;
            _enemyProtected = false;
            _logText.text = $"――― バトル開始（シード {_logicSeed + _battleIndex}）―――\n技を選んでください。";
            _phase = Phase.WaitingInput;
            RefreshHp();
            RefreshButtons();
        }

        /// <summary>
        /// プレイヤーの技選択を受けて1ターン解決する。
        /// 結果は一瞬で確定し（RunTurn）、以降は再生キューが演出として流すだけ。
        /// </summary>
        private void SubmitPlayerAction(Sample_MoveData playerMove)
        {
            if (_phase != Phase.WaitingInput)
            {
                return;
            }

            var enemyMove = DecideEnemyMove();
            _world.Driver.RunTurn(
                new Sample_TurnAction(_world.Charizard, _world.Pikachu, playerMove),
                new Sample_TurnAction(_world.Pikachu, _world.Charizard, enemyMove));

            _queue.EnqueueFrom(Sample_CommandContext.Log(_world.Ctx), ref _cursor);
            _phase = Phase.Playing;
            RefreshButtons();
        }

        /// <summary>敵ピカチュウの行動AI。HPが40以下になったら一度だけ「まもる」、それ以外は攻撃。</summary>
        private Sample_MoveData DecideEnemyMove()
        {
            if (!_enemyProtected && _world.Pikachu.Hp <= 40)
            {
                _enemyProtected = true;
                return _world.Protect;
            }
            return _world.Thunderbolt;
        }

        /// <summary>再生完了時の処理。決着判定を行い、次の入力を受け付ける。</summary>
        private void OnPlaybackFinished()
        {
            RefreshHp();

            if (_world.Charizard.IsFainted || _world.Pikachu.IsFainted)
            {
                var result = _world.Pikachu.IsFainted ? "勝利！" : "敗北…";
                AppendLog($"――― {result} [再戦]で次のシードへ ―――");
                _phase = Phase.Finished;
            }
            else
            {
                AppendLog("技を選んでください。");
                _phase = Phase.WaitingInput;
            }
            RefreshButtons();
        }

        /// <summary>再戦ボタンの処理。シードを変えて世界を作り直す。</summary>
        private void RestartBattle()
        {
            _battleIndex++;
            StartBattle();
        }

        // ================================================================
        // 表示
        // ================================================================

        /// <summary>レコード→再生ステップ（表示して一定時間待つ）への変換。</summary>
        private IPlaybackStep CreateStep(Sample_Record record)
        {
            var line = Sample_CommandBattlePresenter.Format(record);
            return new TimedStep(_playbackInterval, onStart: () => AppendLog(line));
        }

        /// <summary>ログ末尾に1行足す（表示は最新12行まで）。</summary>
        private void AppendLog(string line)
        {
            var lines = _logText.text.Split('\n');
            var keep = Mathf.Min(lines.Length, 11);
            var builder = new System.Text.StringBuilder(512);
            for (var i = lines.Length - keep; i < lines.Length; i++)
            {
                builder.AppendLine(lines[i]);
            }
            builder.Append(line);
            _logText.text = builder.ToString();
        }

        /// <summary>HP表示を現在値へ更新する（再生完了の節目でのみ呼ぶ）。</summary>
        private void RefreshHp()
        {
            var c = _world.Charizard;
            var p = _world.Pikachu;
            _hpText.text = $"あなた: {c.Name} HP {c.Hp}/{c.MaxHp}    てき: {p.Name} HP {p.Hp}/{p.MaxHp}";
        }

        /// <summary>フェーズに応じてButtonの有効状態を切り替える。</summary>
        private void RefreshButtons()
        {
            var waiting = _phase == Phase.WaitingInput;
            _attackButton.interactable = waiting;
            _protectButton.interactable = waiting;
            _restartButton.gameObject.SetActive(_phase == Phase.Finished);
        }

        // ================================================================
        // UI生成（アタッチだけで動くよう、Canvas/Button/Text をコードで組み立てる）
        // ================================================================

        /// <summary>Canvas・EventSystem・ラベル・ボタン一式を生成する。</summary>
        private void BuildUi()
        {
            var canvas = CreateCanvas();
            EnsureEventSystem();

            _hpText = CreateText(canvas, "HpText", new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(900f, 40f), 22);
            _logText = CreateText(canvas, "LogText", new Vector2(0.5f, 0.55f), Vector2.zero, new Vector2(900f, 340f), 20);

            _attackButton = CreateButton(canvas, "ほのおのパンチ", new Vector2(-170f, 70f), () => SubmitPlayerAction(_world.FirePunch));
            _protectButton = CreateButton(canvas, "まもる", new Vector2(170f, 70f), () => SubmitPlayerAction(_world.Protect));
            _restartButton = CreateButton(canvas, "再戦", new Vector2(0f, 70f), RestartBattle);
        }

        /// <summary>ScreenSpaceOverlayのCanvasを生成する。</summary>
        private static RectTransform CreateCanvas()
        {
            var go = new GameObject("Sample_CommandBattleCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            return (RectTransform)go.transform;
        }

        /// <summary>
        /// EventSystemが無ければ生成する。
        /// 新InputSystemのみの設定でも動くよう、InputSystemUIInputModule をリフレクションで探し、
        /// 見つからなければ旧来の StandaloneInputModule を使う（asmdefの依存を増やさないため）。
        /// </summary>
        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            var go = new GameObject("EventSystem", typeof(EventSystem));
            var inputSystemModule = System.Type.GetType(
                "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputSystemModule != null)
            {
                go.AddComponent(inputSystemModule);
            }
            else
            {
                go.AddComponent<StandaloneInputModule>();
            }
        }

        /// <summary>ラベル用のTextを生成する。</summary>
        private static Text CreateText(RectTransform parent, string name,
            Vector2 anchor, Vector2 offset, Vector2 size, int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;

            var text = go.AddComponent<Text>();
            text.font = LoadUiFont();
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = TextAnchor.UpperLeft;
            return text;
        }

        /// <summary>uGUIのButtonを生成する（下端中央基準で配置）。</summary>
        private Button CreateButton(RectTransform parent, string label, Vector2 offset, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject($"Button_{label}", typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = new Vector2(280f, 64f);

            var image = go.GetComponent<Image>();
            image.color = new Color(0.2f, 0.3f, 0.5f, 0.9f);

            var button = go.GetComponent<Button>();
            button.onClick.AddListener(onClick);

            var text = CreateText(rect, "Label", new Vector2(0.5f, 0.5f), Vector2.zero, rect.sizeDelta, 24);
            text.alignment = TextAnchor.MiddleCenter;
            text.text = label;
            return button;
        }

        /// <summary>uGUI Text用の組み込みフォントを取得する（Unityバージョン差に対応）。</summary>
        private static Font LoadUiFont()
        {
            try
            {
                return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            catch (System.Exception)
            {
                return Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
        }
    }
}
