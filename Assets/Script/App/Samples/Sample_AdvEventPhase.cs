using System.Collections.Generic;
using LitMotion;
using Seed.Adv;
using Seed.Data;
using Seed.Flow;
using Seed.Hub;
using Seed.Hub.Contracts;
using Seed.Cameras;
using Seed.Input;
using Seed.UI;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Playables;
using UnityEngine.UI;

namespace Seed.App
{
    /// <summary>
    /// 【サンプル】イベントADVフェーズ（ショップ・会話イベント。ホーム [7]=3D / [8]=2D）。
    ///
    /// [役割分担]
    /// - 進行の真実 … Seed.Adv の AdvPlayer（文字送り・ページ・選択肢の状態機械。Tick で刻む）
    /// - 台本・演出データ … マスターデータ（Sample_EventSpec。ページ/話者/吹き出し/演技/
    ///   カメラ/舞台モード/UIガワ/入力ロック/分岐）
    /// - 本クラス … 台帳を読んで舞台と画面を組み、進行を表示へ写す配線（方針はApp）
    ///
    /// [入力] タップ＝uGUI のボタン（画面全体の透明ボタン＝送り / 選択肢ボタン＝確定）。
    /// キーボード・コントローラ＝Unity 標準の EventSystem ナビゲーション。
    /// ADV操作以外の入力（[B] 退出）はイベントデータの LockOtherInput が停止/許可を決める。
    ///
    /// [ガワ] UI は UiPrefabKey のプレハブ（Sample_AdvUiView 付き）で丸ごと差し替え可能。
    /// 未指定・不備なら内蔵の既定ガワをコードで組む（差し替え口が真実・内蔵は代替）。
    ///
    /// [舞台] StageMode=0 は3D（ワールドにモデル・カメラ演技あり）、
    /// 1 は2D（キャンバスに立ち絵。座標・吹き出しオフセットはキャンバスpx）。
    ///
    /// [演技の実行経路] ビルド済みのブック（1ページ=1タイムライン。Seed/Adv Timeline Build）
    /// があればタイムライン再生が基本で、マーカー通知が ExecuteAct へ届く。無ければ
    /// マスターデータの演技を直接実行する（同じ入口）。演技には寿命
    /// （時間・ページ切替・送り操作）と閉じ命令があり、カメラは手動移動と
    /// 事前定義 Cinemachine カメラの切替（CameraSwitch）の両方に対応する。
    /// </summary>
    public sealed class Sample_AdvEventPhase : GamePhase
    {
        /// <summary>登場キャラ1体ぶんの実体。</summary>
        private sealed class ActorEntry
        {
            /// <summary>実体（3D=舞台上のモデル / 2D=立ち絵のuGUI）。</summary>
            public GameObject Go;

            /// <summary>立ち絵の位置決め（2Dのみ）。</summary>
            public RectTransform Rect;

            /// <summary>モーション再生先（無ければ null）。</summary>
            public Animator Animator;

            /// <summary>表示名（窓の名札用）。</summary>
            public string Name;

            /// <summary>吹き出しの既定オフセット（3D=ワールド / 2D=キャンバスpx）。</summary>
            public Vector3 BubbleOffset;
        }

        /// <summary>吹き出し1枚ぶんの実体（話者用・静的用の両方）。</summary>
        private sealed class BubbleInstance
        {
            /// <summary>実体。</summary>
            public GameObject Root;

            /// <summary>本文。</summary>
            public Text Text;

            /// <summary>位置決め。</summary>
            public RectTransform Rect;

            /// <summary>追従先のキャラID。</summary>
            public int ActorId;

            /// <summary>追従オフセット。</summary>
            public Vector3 Offset;

            /// <summary>自動で消える時刻（0=時間寿命なし）。</summary>
            public float ExpireAt;

            /// <summary>進行のきっかけによる寿命（ページ切替・送り操作）。</summary>
            public AdvActLife Life;
        }

        /// <summary>タイムライン演出1件ぶんの実体（寿命・停止命令の対象）。</summary>
        private sealed class TimelineInstance
        {
            /// <summary>実体（PlayableDirector 入りプレハブの複製）。</summary>
            public GameObject Root;

            /// <summary>起動時のプレハブキー（TimelineStop の対象指定に使う）。</summary>
            public string Key;

            /// <summary>進行のきっかけによる寿命。</summary>
            public AdvActLife Life;

            /// <summary>自動で消える時刻（0=時間寿命なし）。</summary>
            public float ExpireAt;
        }

        /// <summary>発行先のHub（フェーズ遷移命令）。</summary>
        private readonly MessageHub _hub;

        /// <summary>入力（[B] 退出の読取だけ。進行操作は uGUI/EventSystem 側）。</summary>
        private readonly InputRouter _input;

        /// <summary>マスターデータ（イベント台帳）。</summary>
        private readonly MasterDataSet _catalog;

        /// <summary>このフェーズが組んだ全表示物のルート。</summary>
        private GameObject _root;

        /// <summary>3D舞台（背景・モデル）のルート。</summary>
        private GameObject _stageRoot;

        /// <summary>UI一式のルート（ガワ差し替え時に作り直す）。</summary>
        private GameObject _uiRoot;

        /// <summary>UIガワの参照（プレハブ or 内蔵の既定）。</summary>
        private Sample_AdvUiView _view;

        /// <summary>現在のUIガワのキー（変わったときだけ作り直す）。</summary>
        private string _uiKey;

        /// <summary>進行の状態機械。</summary>
        private AdvPlayer _player;

        /// <summary>現在のイベント定義。</summary>
        private Sample_EventSpec _spec;

        /// <summary>登場キャラの台帳（キャラID→実体）。</summary>
        private readonly Dictionary<int, ActorEntry> _actors = new Dictionary<int, ActorEntry>();

        /// <summary>話者の吹き出し（文字送りの流し込み先。ページごとに付け替える）。</summary>
        private BubbleInstance _speakerBubble;

        /// <summary>静的な吹き出し（演技 Bubble で増え、BubbleClear/期限で消える）。</summary>
        private readonly List<BubbleInstance> _staticBubbles = new List<BubbleInstance>();

        /// <summary>再生中のタイムライン演出。</summary>
        private readonly List<TimelineInstance> _timelines = new List<TimelineInstance>();

        /// <summary>切替用の事前定義カメラ（カメラID→Cinemachine カメラ）。</summary>
        private readonly Dictionary<int, CinemachineCamera> _eventCameras =
            new Dictionary<int, CinemachineCamera>();

        /// <summary>既定のカメラID（CameraReset・寿命到達時の戻り先）。</summary>
        private int _defaultCameraId;

        /// <summary>カメラ演出の寿命（Keep=残す）。</summary>
        private AdvActLife _cameraLife = AdvActLife.Keep;

        /// <summary>手動カメラの既定位置（CameraReset の戻り先）。</summary>
        private Vector3 _cameraHomePosition;

        /// <summary>手動カメラの既定向き。</summary>
        private Quaternion _cameraHomeRotation;

        /// <summary>ビルド済みのブック（無ければ null＝マスターデータ直接実行）。</summary>
        private Sample_AdvBookAsset _book;

        /// <summary>ブックのページを再生するディレクター。</summary>
        private PlayableDirector _director;

        /// <summary>実行中の移動・カメラ演出（イベント切替・退場時に畳む）。</summary>
        private readonly List<MotionHandle> _motions = new List<MotionHandle>();

        /// <summary>選択肢ボタン購読の後始末袋（イベント切替ごとに畳む）。</summary>
        private readonly List<System.IDisposable> _clickSubscriptions =
            new List<System.IDisposable>();

        /// <summary>送りボタンの購読（UIガワの作り直しごとに張り替える）。</summary>
        private System.IDisposable _tapSubscription;

        /// <summary>選択肢ボタン（Choosing 中だけ表示）。</summary>
        private readonly List<SeedButton> _choiceButtons = new List<SeedButton>();

        /// <summary>いま文字を流し込んでいる先（窓の本文 or 話者の吹き出し）。</summary>
        private Text _activeText;

        /// <summary>表示済み文字数のキャッシュ（変化したときだけ Substring する）。</summary>
        private int _visibleCache = -1;

        /// <summary>▼の明滅・吹き出し期限用の経過時間。</summary>
        private float _time;

        /// <summary>終了処理の予約（次イベントID。0=ホームへ。イベント購読中の即遷移を避ける）。</summary>
        private int? _pendingResult;

        /// <summary>Sample_AdvEventPhase を生成する。</summary>
        public Sample_AdvEventPhase(MessageHub hub, InputRouter input, MasterDataSet catalog)
        {
            _hub = hub;
            _input = input;
            _catalog = catalog;
        }

        /// <summary>このフェーズのID。</summary>
        public override PhaseId Id => Sample_PhaseIds.Event;

        /// <summary>舞台と画面を組み立てる（payload=開始イベントID。0なら店）。</summary>
        public override void OnEnter(int payload)
        {
            _root = new GameObject("AdvEventPhase");
            _uiKey = null;
            EnsureEventSystem();
            var directorGo = new GameObject("AdvDirector",
                typeof(PlayableDirector), typeof(Sample_AdvDirectorReceiver));
            directorGo.transform.SetParent(_root.transform, false);
            _director = directorGo.GetComponent<PlayableDirector>();
            directorGo.GetComponent<Sample_AdvDirectorReceiver>().ActReceived = ExecuteAct;
            LoadEvent(payload != 0 ? payload : Sample_MasterCatalog.EventShop);
        }

        /// <summary>進行を刻み、表示へ写す。</summary>
        public override void Tick(float deltaTime)
        {
            // [B] 退出はイベントデータが許可しているときだけ（LockOtherInput=他入力の停止）
            if (_spec != null && !_spec.LockOtherInput
                && _input.WasPressedThisFrame(ActionId.Cancel))
            {
                _hub.PublishCommand(new ChangePhaseCommand(Sample_PhaseIds.Home, 0));
                return;
            }

            // 終了予約の消化（イベント連鎖 or ホームへ）
            if (_pendingResult.HasValue)
            {
                var next = _pendingResult.Value;
                _pendingResult = null;
                if (next == 0)
                {
                    _hub.PublishCommand(new ChangePhaseCommand(Sample_PhaseIds.Home, 0));
                }
                else
                {
                    LoadEvent(next);
                }
                return;
            }

            _time += deltaTime;
            _player?.Tick(deltaTime);
            SyncText();
            SyncBubbles();
            SyncTimelines();
            SyncPageMark();
        }

        /// <summary>組み立ての逆順で片付ける。</summary>
        public override void OnExit()
        {
            CancelMotions();
            ClearClickSubscriptions();
            _tapSubscription?.Dispose();
            _tapSubscription = null;
            _actors.Clear();
            _staticBubbles.Clear();
            _timelines.Clear();
            _eventCameras.Clear();
            _book = null;
            _director = null;
            _speakerBubble = null;
            _choiceButtons.Clear();
            _player = null;
            _spec = null;
            _view = null;
            _activeText = null;
            _uiRoot = null;
            _stageRoot = null;
            if (_root != null)
            {
                Object.Destroy(_root);
                _root = null;
            }
        }

        // ================================================================
        // イベントの読込と進行
        // ================================================================

        /// <summary>イベント1本を台帳から読み込んで開始する（連鎖時もここへ戻る）。</summary>
        private void LoadEvent(int eventId)
        {
            _spec = _catalog.Get<Sample_EventSpec>(eventId);
            _book = Resources.Load<Sample_AdvBookAsset>($"AdvBooks/Event{eventId}");

            _director?.Stop();
            CancelMotions();
            ClearClickSubscriptions();
            _timelines.Clear(); // 実体は舞台ごと破棄される
            _cameraLife = AdvActLife.Keep;
            BuildUi();
            BuildStage();
            BuildActors();
            BuildEventCameras(); // 注視先（キャラ）が揃ってから置く
            BuildChoices();
            ClearAllBubbles();

            _player = new AdvPlayer(_spec.BuildScript(), _spec.CharsPerSecond);
            _player.PageChanged += OnPageEntered;
            _player.StateChanged += RefreshInteraction;
            _player.Finished += resultId =>
                _pendingResult = resultId != 0 ? resultId : _spec.NextEventId;

            _time = 0f;
            PlayPage(); // 1ページ目の演技（ブックがあればタイムライン再生・無ければ直接実行）
            RefreshSpeech();
            RefreshInteraction();
        }

        /// <summary>ページが変わった（演技の実行と表示先の切替）。</summary>
        private void OnPageEntered()
        {
            CloseByLife(AdvActLife.Page);
            PlayPage();
            RefreshSpeech();
        }

        /// <summary>
        /// 現在ページの演技を開始する。ビルド済みのブック（1ページ=1タイムライン）が
        /// あればタイムライン再生で行い、無ければマスターデータの演技を直接実行する
        /// （タイムラインが基本経路・直接実行はフォールバック）。
        /// </summary>
        private void PlayPage()
        {
            var pageIndex = _player.PageIndex;
            if (_book != null && _director != null && _book.Pages != null
                && pageIndex < _book.Pages.Length && _book.Pages[pageIndex] != null)
            {
                _director.Stop();
                _director.playableAsset = _book.Pages[pageIndex];
                _director.time = 0;
                _director.Play(); // 演技はマーカー通知で ExecuteAct へ届く
                return;
            }
            ExecuteActs(_player.CurrentPage);
        }

        /// <summary>送り操作（タップ・決定）。送り寿命の演出を閉じてから進める。</summary>
        private void OnAdvanceInput()
        {
            CloseByLife(AdvActLife.Advance);
            _player?.Advance();
        }

        /// <summary>ページの演技（舞台指示）をまとめて実行する（フォールバック経路）。</summary>
        private void ExecuteActs(AdvPage page)
        {
            for (var i = 0; i < page.Acts.Count; i++)
            {
                ExecuteAct(page.Acts[i]);
            }
        }

        /// <summary>演技1件を実行する（タイムラインのマーカー経由でも直接実行でも同じ入口）。</summary>
        private void ExecuteAct(AdvAct act)
        {
            switch (act.Kind)
            {
                case AdvActKind.Camera:
                    MoveCamera(act);
                    return; // ここから下は対象キャラ必須ではない命令
                case AdvActKind.CameraSwitch:
                    SwitchEventCamera(act.ActorId, act.Seconds, act.Life);
                    return;
                case AdvActKind.CameraReset:
                    ResetCamera(act.Seconds);
                    return;
                case AdvActKind.Timeline:
                    PlayTimeline(act);
                    return;
                case AdvActKind.TimelineStop:
                    StopTimelines(act.Key);
                    return;
                case AdvActKind.BubbleClear:
                    ClearStaticBubbles(act.ActorId);
                    return;
                case AdvActKind.Signal:
                    // 台本→コードの合図。意味づけは購読側（報酬付与・フラグ更新など）
                    _hub.Publish(new Sample_AdvSignal(_spec.Id, act.Key, act.ActorId, act.X));
                    return;
            }
            if (!_actors.TryGetValue(act.ActorId, out var actor) || actor.Go == null)
            {
                Debug.LogWarning($"[Adv] 演技の対象キャラが居ない: actorId={act.ActorId}（イベント {_spec.Id}）");
                return;
            }
            switch (act.Kind)
            {
                case AdvActKind.Move:
                    MoveActor(actor, act);
                    break;
                case AdvActKind.Motion:
                    actor.Animator?.CrossFadeInFixedTime(act.Key, 0.15f);
                    break;
                case AdvActKind.Model:
                    SwapModel(actor, act.Key, act.SubKey);
                    break;
                case AdvActKind.Show:
                    actor.Go.SetActive(true);
                    break;
                case AdvActKind.Hide:
                    actor.Go.SetActive(false);
                    break;
                case AdvActKind.Bubble:
                    SpawnStaticBubble(act, actor);
                    break;
            }
        }

        /// <summary>キャラを移動させる（3D=ワールド / 2D=キャンバスpx。Seconds=0 は瞬間）。</summary>
        private void MoveActor(ActorEntry actor, AdvAct act)
        {
            if (actor.Rect != null)
            {
                // 2D: 立ち絵のアンカー位置を動かす（スライドイン等）
                var target2d = new Vector3(act.X, act.Y, 0f);
                if (act.Seconds <= 0f)
                {
                    actor.Rect.anchoredPosition = target2d;
                    return;
                }
                var rect = actor.Rect;
                _motions.Add(LMotion.Create((Vector3)rect.anchoredPosition, target2d, act.Seconds)
                    .WithEase(Ease.InOutSine)
                    .Bind(position =>
                    {
                        if (rect != null)
                        {
                            rect.anchoredPosition = position;
                        }
                    }));
                return;
            }

            // 3D: 舞台上の移動（移動中は進行方向、着いたら正面へ）
            var transform = actor.Go.transform;
            var target = new Vector3(act.X, act.Y, act.Z);
            if (act.Seconds <= 0f)
            {
                transform.position = target;
                return;
            }
            var direction = target - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(direction);
            }
            var entry = actor;
            _motions.Add(LMotion.Create(transform.position, target, act.Seconds)
                .WithEase(Ease.InOutSine)
                .WithOnComplete(() =>
                {
                    if (entry.Go != null)
                    {
                        entry.Go.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                    }
                })
                .Bind(position =>
                {
                    if (entry.Go != null)
                    {
                        entry.Go.transform.position = position;
                    }
                }));
        }

        /// <summary>カメラを手動で動かす（3D舞台・カメラ切替を定義していないイベント用）。</summary>
        private void MoveCamera(AdvAct act)
        {
            if (_spec.StageMode != 0)
            {
                Debug.LogWarning($"[Adv] カメラ演技は2D舞台では使えない（イベント {_spec.Id}）");
                return;
            }
            if (_eventCameras.Count > 0)
            {
                Debug.LogWarning($"[Adv] カメラ切替（CamIds）定義中は CameraSwitch を使う（イベント {_spec.Id}）");
                return;
            }
            _cameraLife = act.Life;
            var camera = Camera.main;
            if (camera == null)
            {
                return;
            }
            var endPosition = new Vector3(act.X, act.Y, act.Z);
            var endRotation = Quaternion.identity; // 注視キャラ無し=正面
            if (act.ActorId != 0 && _actors.TryGetValue(act.ActorId, out var actor) && actor.Go != null)
            {
                var focus = actor.Go.transform.position + Vector3.up * 1.4f; // 胸〜顔の高さ
                endRotation = Quaternion.LookRotation(focus - endPosition);
            }
            if (act.Seconds <= 0f)
            {
                camera.transform.SetPositionAndRotation(endPosition, endRotation);
                return;
            }
            var startPosition = camera.transform.position;
            var startRotation = camera.transform.rotation;
            _motions.Add(LMotion.Create(0f, 1f, act.Seconds)
                .WithEase(Ease.InOutSine)
                .Bind(t =>
                {
                    if (camera != null)
                    {
                        camera.transform.SetPositionAndRotation(
                            Vector3.Lerp(startPosition, endPosition, t),
                            Quaternion.Slerp(startRotation, endRotation, t));
                    }
                }));
        }

        /// <summary>事前定義した Cinemachine カメラへ切り替える（ブレンドは Brain）。</summary>
        private void SwitchEventCamera(int cameraId, float blendSeconds, AdvActLife life)
        {
            if (!_eventCameras.TryGetValue(cameraId, out var target))
            {
                Debug.LogWarning($"[Adv] 切替先カメラが未定義: id={cameraId}（イベント {_spec.Id}）");
                return;
            }
            var main = Camera.main;
            if (main != null)
            {
                CameraRigBuilder.EnsureBrain(main, blendSeconds > 0f ? blendSeconds : 0.5f);
            }
            foreach (var pair in _eventCameras)
            {
                pair.Value.Priority = pair.Value == target ? 10 : 0;
            }
            _cameraLife = cameraId == _defaultCameraId ? AdvActLife.Keep : life;
        }

        /// <summary>カメラを既定へ戻す（閉じ命令・寿命到達時。切替定義があれば既定カメラへ）。</summary>
        private void ResetCamera(float seconds)
        {
            _cameraLife = AdvActLife.Keep;
            if (_spec.StageMode != 0)
            {
                return;
            }
            if (_eventCameras.Count > 0)
            {
                SwitchEventCamera(_defaultCameraId, seconds > 0f ? seconds : 0.5f, AdvActLife.Keep);
                return;
            }
            var camera = Camera.main;
            if (camera == null)
            {
                return;
            }
            if (seconds <= 0f)
            {
                camera.transform.SetPositionAndRotation(_cameraHomePosition, _cameraHomeRotation);
                return;
            }
            var startPosition = camera.transform.position;
            var startRotation = camera.transform.rotation;
            var endPosition = _cameraHomePosition;
            var endRotation = _cameraHomeRotation;
            _motions.Add(LMotion.Create(0f, 1f, seconds)
                .WithEase(Ease.InOutSine)
                .Bind(t =>
                {
                    if (camera != null)
                    {
                        camera.transform.SetPositionAndRotation(
                            Vector3.Lerp(startPosition, endPosition, t),
                            Quaternion.Slerp(startRotation, endRotation, t));
                    }
                }));
        }

        /// <summary>タイムライン演出を起動する（PlayableDirector 入りプレハブをデータから）。</summary>
        private void PlayTimeline(AdvAct act)
        {
            var prefab = Resources.Load<GameObject>(act.Key);
            if (prefab == null)
            {
                Debug.LogWarning($"[Adv] タイムラインのプレハブが見つからない: {act.Key}（イベント {_spec.Id}）");
                return;
            }
            var instance = Object.Instantiate(prefab, _stageRoot.transform);
            var director = instance.GetComponentInChildren<PlayableDirector>();
            if (director != null)
            {
                director.Play(); // 演出の中身（トラック・バインド）はプレハブ内で完結させる契約
            }
            else
            {
                Debug.LogWarning($"[Adv] プレハブに PlayableDirector が無い: {act.Key}");
            }
            _timelines.Add(new TimelineInstance
            {
                Root = instance,
                Key = act.Key,
                Life = act.Life,
                ExpireAt = act.Seconds > 0f ? _time + act.Seconds : 0f,
            });
        }

        /// <summary>タイムライン演出を止めて片付ける（key 空=全部。Timeline の閉じ命令）。</summary>
        private void StopTimelines(string key)
        {
            for (var i = _timelines.Count - 1; i >= 0; i--)
            {
                if (!string.IsNullOrEmpty(key) && _timelines[i].Key != key)
                {
                    continue;
                }
                Object.Destroy(_timelines[i].Root);
                _timelines.RemoveAt(i);
            }
        }

        /// <summary>指定の寿命を迎えた演出（吹き出し・タイムライン・カメラ）を閉じる。</summary>
        private void CloseByLife(AdvActLife life)
        {
            for (var i = _staticBubbles.Count - 1; i >= 0; i--)
            {
                if (_staticBubbles[i].Life == life)
                {
                    Object.Destroy(_staticBubbles[i].Root);
                    _staticBubbles.RemoveAt(i);
                }
            }
            for (var i = _timelines.Count - 1; i >= 0; i--)
            {
                if (_timelines[i].Life == life)
                {
                    Object.Destroy(_timelines[i].Root);
                    _timelines.RemoveAt(i);
                }
            }
            if (_cameraLife == life)
            {
                ResetCamera(0.5f);
            }
        }

        /// <summary>モデル/立ち絵を読み替える（位置は引き継ぐ）。</summary>
        private void SwapModel(ActorEntry actor, string prefabKey, string controllerKey)
        {
            if (actor.Rect != null)
            {
                var anchored = actor.Rect.anchoredPosition;
                Object.Destroy(actor.Go);
                actor.Go = CreatePortrait(prefabKey, actor.Name, anchored);
                actor.Rect = (RectTransform)actor.Go.transform;
            }
            else
            {
                var position = actor.Go.transform.position;
                var rotation = actor.Go.transform.rotation;
                Object.Destroy(actor.Go);
                actor.Go = CreateActorModel(prefabKey, controllerKey, position, rotation);
            }
            actor.Animator = actor.Go.GetComponentInChildren<Animator>();
        }

        // ================================================================
        // 吹き出し（0〜複数を同時に出せる）
        // ================================================================

        /// <summary>静的な吹き出しを1枚出す（話者の吹き出しとは独立に重なる）。</summary>
        private void SpawnStaticBubble(AdvAct act, ActorEntry actor)
        {
            var offset = new Vector3(act.X, act.Y, act.Z);
            if (offset == Vector3.zero)
            {
                offset = actor.BubbleOffset; // 全て0なら既定のオフセット
            }
            var bubble = CreateBubbleInstance();
            bubble.ActorId = act.ActorId;
            bubble.Offset = offset;
            bubble.Text.text = act.Key; // 静的な吹き出しは文字送りせず全文
            bubble.ExpireAt = act.Seconds > 0f ? _time + act.Seconds : 0f;
            bubble.Life = act.Life;
            bubble.Root.SetActive(true);
            _staticBubbles.Add(bubble);
        }

        /// <summary>静的な吹き出しを消す（actorId=0 は全キャラぶん）。</summary>
        private void ClearStaticBubbles(int actorId)
        {
            for (var i = _staticBubbles.Count - 1; i >= 0; i--)
            {
                if (actorId != 0 && _staticBubbles[i].ActorId != actorId)
                {
                    continue;
                }
                Object.Destroy(_staticBubbles[i].Root);
                _staticBubbles.RemoveAt(i);
            }
        }

        /// <summary>吹き出しを全て畳む（イベント切替時）。</summary>
        private void ClearAllBubbles()
        {
            ClearStaticBubbles(0);
            if (_speakerBubble != null)
            {
                Object.Destroy(_speakerBubble.Root);
                _speakerBubble = null;
            }
        }

        /// <summary>吹き出しの雛形から1枚複製する。</summary>
        private BubbleInstance CreateBubbleInstance()
        {
            var root = Object.Instantiate(_view.BubbleTemplate, _view.BubbleLayer);
            return new BubbleInstance
            {
                Root = root,
                Rect = (RectTransform)root.transform,
                Text = root.GetComponentInChildren<Text>(true),
            };
        }

        /// <summary>吹き出しの追従と期限切れ（3D=スクリーン投影 / 2D=立ち絵の位置＋オフセット）。</summary>
        private void SyncBubbles()
        {
            for (var i = _staticBubbles.Count - 1; i >= 0; i--)
            {
                var bubble = _staticBubbles[i];
                if (bubble.ExpireAt > 0f && _time >= bubble.ExpireAt)
                {
                    Object.Destroy(bubble.Root);
                    _staticBubbles.RemoveAt(i);
                    continue;
                }
                PlaceBubble(bubble);
            }
            if (_speakerBubble != null && _speakerBubble.Root.activeSelf)
            {
                PlaceBubble(_speakerBubble);
            }
        }

        /// <summary>タイムライン演出の時間寿命を見張る。</summary>
        private void SyncTimelines()
        {
            for (var i = _timelines.Count - 1; i >= 0; i--)
            {
                if (_timelines[i].ExpireAt > 0f && _time >= _timelines[i].ExpireAt)
                {
                    Object.Destroy(_timelines[i].Root);
                    _timelines.RemoveAt(i);
                }
            }
        }

        /// <summary>吹き出し1枚を追従先へ置く。</summary>
        private void PlaceBubble(BubbleInstance bubble)
        {
            if (!_actors.TryGetValue(bubble.ActorId, out var actor) || actor.Go == null)
            {
                return;
            }
            if (actor.Rect != null)
            {
                // 2D: 立ち絵の画面位置＋オフセット（キャンバスの拡縮を掛ける）
                var scale = _view.GetComponentInParent<Canvas>()?.scaleFactor ?? 1f;
                bubble.Rect.position = actor.Rect.position
                    + (Vector3)(new Vector2(bubble.Offset.x, bubble.Offset.y) * scale);
                return;
            }
            var camera = Camera.main;
            if (camera == null)
            {
                return;
            }
            var screen = camera.WorldToScreenPoint(actor.Go.transform.position + bubble.Offset);
            bubble.Root.SetActive(screen.z > 0f); // カメラ背後では出さない
            bubble.Rect.position = screen;
        }

        // ================================================================
        // 表示の同期（艶）
        // ================================================================

        /// <summary>見せ方（窓/吹き出し）と名札を現在ページに合わせる。</summary>
        private void RefreshSpeech()
        {
            var page = _player.CurrentPage;
            var speaker = _actors.TryGetValue(page.SpeakerId, out var entry) ? entry : null;
            var useBubble = page.Style == AdvSpeechStyle.Bubble && speaker != null;

            if (useBubble)
            {
                _speakerBubble ??= CreateBubbleInstance();
                _speakerBubble.ActorId = page.SpeakerId;
                _speakerBubble.Offset = speaker.BubbleOffset;
                _speakerBubble.Text.text = "";
                _speakerBubble.Root.SetActive(true);
                _activeText = _speakerBubble.Text;
            }
            else
            {
                _speakerBubble?.Root.SetActive(false);
                _activeText = _view.BodyText;
            }
            _view.WindowRoot.SetActive(!useBubble);
            _view.PlateText.text = speaker != null ? speaker.Name : _spec.Title;
            _view.BodyText.text = "";
            _visibleCache = -1;
        }

        /// <summary>文字送りの表示反映（文字数が変わったときだけ組み立てる）。</summary>
        private void SyncText()
        {
            if (_player == null || _activeText == null || _visibleCache == _player.VisibleLength)
            {
                return;
            }
            _visibleCache = _player.VisibleLength;
            _activeText.text = _player.PageText.Substring(0, _visibleCache);
        }

        /// <summary>送り待ちマーク（▼）の表示と明滅。</summary>
        private void SyncPageMark()
        {
            if (_player == null)
            {
                return;
            }
            var visible = _player.State == AdvState.PageComplete && _view.WindowRoot.activeSelf;
            _view.PageMark.enabled = visible;
            if (visible)
            {
                var color = _view.PageMark.color;
                color.a = 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(_time * 4f));
                _view.PageMark.color = color;
            }
        }

        /// <summary>
        /// 操作系の切替（送り⇄選択）。選択中は選択肢ボタンの先頭を選択状態にし、
        /// それ以外は透明の送りボタンを選択状態にする——Enter・ゲームパッド決定は
        /// EventSystem がそのままボタンの Submit として届ける（Unity 標準機能に委譲）。
        /// </summary>
        private void RefreshInteraction()
        {
            if (_player == null)
            {
                return;
            }
            var choosing = _player.State == AdvState.Choosing;
            _view.ChoicesRoot.gameObject.SetActive(choosing);
            _view.TapCatcher.gameObject.SetActive(!choosing && _player.State != AdvState.Finished);

            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return;
            }
            if (choosing && _choiceButtons.Count > 0)
            {
                eventSystem.SetSelectedGameObject(_choiceButtons[0].gameObject);
            }
            else if (_view.TapCatcher.gameObject.activeSelf)
            {
                eventSystem.SetSelectedGameObject(_view.TapCatcher.gameObject);
            }
        }

        // ================================================================
        // 舞台の構築（3D / 2D）
        // ================================================================

        /// <summary>舞台を組む（3D=背景・カメラ / 2D=立ち絵の背景）。</summary>
        private void BuildStage()
        {
            if (_stageRoot != null)
            {
                Object.Destroy(_stageRoot);
            }
            _stageRoot = new GameObject("AdvStage");
            _stageRoot.transform.SetParent(_root.transform, false);

            // 立ち絵層はイベントごとに畳む（2D↔3Dの切替にも耐える）
            for (var i = _view.PortraitLayer.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(_view.PortraitLayer.GetChild(i).gameObject);
            }

            if (_spec.StageMode != 0)
            {
                Build2DBackdrop();
                return;
            }

            // 3D: カメラを定位置へ（カメラ演技・CameraReset の戻り先でもある）
            _cameraHomePosition = new Vector3(0f, 1.35f, 0f);
            _cameraHomeRotation = Quaternion.identity;
            var camera = Camera.main;
            if (camera != null)
            {
                camera.transform.SetPositionAndRotation(_cameraHomePosition, _cameraHomeRotation);
            }

            if (!string.IsNullOrEmpty(_spec.BackgroundPrefabKey))
            {
                var prefab = Resources.Load<GameObject>(_spec.BackgroundPrefabKey);
                if (prefab != null)
                {
                    Object.Instantiate(prefab, _stageRoot.transform);
                    return;
                }
                Debug.LogWarning($"[Adv] 背景プレハブが見つからない: {_spec.BackgroundPrefabKey}（色板で代替）");
            }
            CreateTintedQuad("Backdrop", new Vector3(0f, 2.2f, 4.6f), Quaternion.identity,
                new Vector3(16f, 7f, 1f), new Color(0.13f, 0.12f, 0.18f));
            CreateTintedQuad("Floor", new Vector3(0f, 0f, 3f), Quaternion.Euler(90f, 0f, 0f),
                new Vector3(16f, 10f, 1f), new Color(0.2f, 0.18f, 0.16f));
        }

        /// <summary>
        /// 切替用の Cinemachine カメラをマスターデータどおりに置く（3D舞台・CamIds 指定時のみ）。
        /// 先頭のIDが既定カメラになり、CameraSwitch/CameraReset・寿命がここを行き来する。
        /// </summary>
        private void BuildEventCameras()
        {
            _eventCameras.Clear();
            if (_spec.CamIds.Length == 0 || _spec.StageMode != 0)
            {
                return;
            }
            for (var i = 0; i < _spec.CamIds.Length; i++)
            {
                var position = new Vector3(_spec.CamX[i], _spec.CamY[i], _spec.CamZ[i]);
                var look = position + Vector3.forward; // 注視キャラ無し=正面
                if (_spec.CamLookActors[i] != 0
                    && _actors.TryGetValue(_spec.CamLookActors[i], out var actor) && actor.Go != null)
                {
                    look = actor.Go.transform.position + Vector3.up * 1.4f;
                }
                var vcam = CameraRigBuilder.CreateFixed($"AdvCam{_spec.CamIds[i]}", position, look,
                    _stageRoot.transform);
                vcam.Priority = 0;
                _eventCameras[_spec.CamIds[i]] = vcam;
            }
            _defaultCameraId = _spec.CamIds[0];
            SwitchEventCamera(_defaultCameraId, 0f, AdvActLife.Keep);
        }

        /// <summary>2Dの背景（プレハブ or 全画面の色板）を立ち絵層の最背面へ敷く。</summary>
        private void Build2DBackdrop()
        {
            GameObject backdrop;
            var prefab = string.IsNullOrEmpty(_spec.BackgroundPrefabKey)
                ? null
                : Resources.Load<GameObject>(_spec.BackgroundPrefabKey);
            if (prefab != null)
            {
                backdrop = Object.Instantiate(prefab, _view.PortraitLayer);
            }
            else
            {
                backdrop = new GameObject("Backdrop2D", typeof(RectTransform), typeof(Image));
                backdrop.transform.SetParent(_view.PortraitLayer, false);
                Stretch((RectTransform)backdrop.transform);
                var image = backdrop.GetComponent<Image>();
                image.color = new Color(0.09f, 0.1f, 0.15f);
                image.raycastTarget = false;
            }
            backdrop.transform.SetAsFirstSibling();
        }

        /// <summary>色付きの板を1枚置く（3D舞台の内蔵フォールバック）。</summary>
        private void CreateTintedQuad(string name, Vector3 position, Quaternion rotation,
            Vector3 scale, Color color)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            Object.Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(_stageRoot.transform, false);
            quad.transform.SetLocalPositionAndRotation(position, rotation);
            quad.transform.localScale = scale;
            Seed.Character.RendererTint.Set(quad.GetComponent<Renderer>(), color);
        }

        /// <summary>登場キャラをマスターデータどおりに並べる（3D=モデル / 2D=立ち絵）。</summary>
        private void BuildActors()
        {
            _actors.Clear();
            for (var i = 0; i < _spec.ActorIds.Length; i++)
            {
                var entry = new ActorEntry
                {
                    Name = _spec.ActorNames[i],
                    BubbleOffset = new Vector3(_spec.BubbleX[i], _spec.BubbleY[i], _spec.BubbleZ[i]),
                };
                if (_spec.StageMode != 0)
                {
                    entry.Go = CreatePortrait(_spec.ActorPrefabs[i], _spec.ActorNames[i],
                        new Vector2(_spec.ActorX[i], _spec.ActorY[i]));
                    entry.Rect = (RectTransform)entry.Go.transform;
                }
                else
                {
                    entry.Go = CreateActorModel(_spec.ActorPrefabs[i], _spec.ActorControllers[i],
                        new Vector3(_spec.ActorX[i], _spec.ActorY[i], _spec.ActorZ[i]),
                        Quaternion.Euler(0f, 180f, 0f));
                }
                entry.Animator = entry.Go.GetComponentInChildren<Animator>();
                _actors[_spec.ActorIds[i]] = entry;
            }
        }

        /// <summary>3Dのキャラモデル1体を組む（プレハブ→無ければカプセルで代替）。</summary>
        private GameObject CreateActorModel(string prefabKey, string controllerKey,
            Vector3 position, Quaternion rotation)
        {
            GameObject go;
            var prefab = string.IsNullOrEmpty(prefabKey)
                ? null
                : Resources.Load<GameObject>(prefabKey);
            if (prefab != null)
            {
                go = Object.Instantiate(prefab, _stageRoot.transform);
            }
            else
            {
                if (!string.IsNullOrEmpty(prefabKey))
                {
                    Debug.LogWarning($"[Adv] キャラプレハブが見つからない: {prefabKey}（カプセルで代替）");
                }
                go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                Object.Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(_stageRoot.transform, false);
                go.transform.localScale = new Vector3(0.6f, 0.9f, 0.6f);
                position += Vector3.up * 0.9f; // カプセルは中心原点
                Seed.Character.RendererTint.Set(go.GetComponent<Renderer>(),
                    new Color(0.5f, 0.55f, 0.7f));
            }
            go.transform.SetPositionAndRotation(position, rotation);

            if (!string.IsNullOrEmpty(controllerKey))
            {
                var controller = Resources.Load<RuntimeAnimatorController>(controllerKey);
                var animator = go.GetComponentInChildren<Animator>();
                if (controller != null && animator != null)
                {
                    animator.runtimeAnimatorController = controller; // 既定ステート（Idle）が流れる
                    animator.applyRootMotion = false;
                }
            }
            return go;
        }

        /// <summary>2Dの立ち絵1枚を組む（uGUIプレハブ→無ければ色板＋名前で代替）。</summary>
        private GameObject CreatePortrait(string prefabKey, string displayName, Vector2 anchored)
        {
            GameObject go;
            var prefab = string.IsNullOrEmpty(prefabKey)
                ? null
                : Resources.Load<GameObject>(prefabKey);
            if (prefab != null && prefab.GetComponent<RectTransform>() != null)
            {
                go = Object.Instantiate(prefab, _view.PortraitLayer);
            }
            else
            {
                if (!string.IsNullOrEmpty(prefabKey))
                {
                    Debug.LogWarning($"[Adv] 立ち絵プレハブが見つからない/RectTransformが無い: {prefabKey}（色板で代替）");
                }
                go = new GameObject($"Portrait_{displayName}", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(_view.PortraitLayer, false);
                var rect = (RectTransform)go.transform;
                rect.sizeDelta = new Vector2(420f, 720f);
                var image = go.GetComponent<Image>();
                image.color = new Color(0.28f, 0.32f, 0.45f, 0.95f);
                image.raycastTarget = false;
                var label = CreateText(rect, "Name", 40, Color.white, TextAnchor.MiddleCenter);
                label.text = displayName;
                Stretch((RectTransform)label.transform);
            }
            ((RectTransform)go.transform).anchoredPosition = anchored;
            return go;
        }

        // ================================================================
        // 画面（uGUI）の構築——プレハブのガワ or 内蔵の既定ガワ
        // ================================================================

        /// <summary>EventSystem が無ければ作る（uGUI のクリック・ナビゲーションの前提）。</summary>
        private void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }
            var eventSystem = new GameObject("EventSystem",
                typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.transform.SetParent(_root.transform, false);
        }

        /// <summary>
        /// UIガワを用意する。UiPrefabKey のプレハブ（Sample_AdvUiView 付き）があれば
        /// それを使い、無ければ内蔵の既定ガワをコードで組む——**差し替え口が正・内蔵は代替**。
        /// </summary>
        private void BuildUi()
        {
            if (_uiKey == _spec.UiPrefabKey && _view != null)
            {
                return; // 同じガワなら作り直さない
            }
            _uiKey = _spec.UiPrefabKey;
            _tapSubscription?.Dispose();
            _tapSubscription = null;
            _speakerBubble = null;
            _staticBubbles.Clear();
            if (_uiRoot != null)
            {
                Object.Destroy(_uiRoot);
            }

            if (!string.IsNullOrEmpty(_spec.UiPrefabKey))
            {
                var prefab = Resources.Load<GameObject>(_spec.UiPrefabKey);
                var view = prefab != null ? prefab.GetComponent<Sample_AdvUiView>() : null;
                if (view != null)
                {
                    _uiRoot = Object.Instantiate(prefab, _root.transform);
                    _view = _uiRoot.GetComponent<Sample_AdvUiView>();
                    if (!_view.IsComplete())
                    {
                        Debug.LogWarning($"[Adv] UIプレハブの参照が欠けている: {_spec.UiPrefabKey}（内蔵ガワで代替）");
                        Object.Destroy(_uiRoot);
                        _view = null;
                    }
                }
                else
                {
                    Debug.LogWarning($"[Adv] UIプレハブが見つからない/Sample_AdvUiView が無い: {_spec.UiPrefabKey}（内蔵ガワで代替）");
                }
            }
            if (_view == null || _uiRoot == null)
            {
                BuildDefaultUi();
            }
            _tapSubscription = _view.TapCatcher.OnClick(OnAdvanceInput);
        }

        /// <summary>内蔵の既定ガワを組む（プレハブ側と同じ構造・同じ結び付け）。</summary>
        private void BuildDefaultUi()
        {
            _uiRoot = new GameObject("AdvCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _uiRoot.transform.SetParent(_root.transform, false);
            var canvas = _uiRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = _uiRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            _view = _uiRoot.AddComponent<Sample_AdvUiView>();

            // 1) 画面全体の透明な「送り」ボタン（最背面）
            var catcherGo = new GameObject("TapCatcher", typeof(RectTransform), typeof(Image));
            catcherGo.transform.SetParent(_uiRoot.transform, false);
            Stretch((RectTransform)catcherGo.transform);
            catcherGo.GetComponent<Image>().color = Color.clear; // 透明でもタップは受かる
            var tapCatcher = catcherGo.AddComponent<SeedButton>();
            tapCatcher.transition = Selectable.Transition.None;
            tapCatcher.CooldownSeconds = 0.05f; // 文字送りの連打を邪魔しない
            tapCatcher.navigation = new Navigation { mode = Navigation.Mode.None };
            _view.TapCatcher = tapCatcher;

            // 2) 立ち絵層（2D舞台用。3Dでは空のまま）
            var portraitGo = new GameObject("Portraits", typeof(RectTransform));
            portraitGo.transform.SetParent(_uiRoot.transform, false);
            Stretch((RectTransform)portraitGo.transform);
            _view.PortraitLayer = (RectTransform)portraitGo.transform;

            // 3) 吹き出し層＋雛形
            var bubbleGo = new GameObject("Bubbles", typeof(RectTransform));
            bubbleGo.transform.SetParent(_uiRoot.transform, false);
            Stretch((RectTransform)bubbleGo.transform);
            _view.BubbleLayer = (RectTransform)bubbleGo.transform;
            _view.BubbleTemplate = BuildBubbleTemplate(_view.BubbleLayer);

            // 4) 固定テキストボックス（窓）
            var windowGo = new GameObject("Window", typeof(RectTransform), typeof(Image));
            windowGo.transform.SetParent(_uiRoot.transform, false);
            var windowRect = (RectTransform)windowGo.transform;
            windowRect.anchorMin = new Vector2(0f, 0f);
            windowRect.anchorMax = new Vector2(1f, 0f);
            windowRect.pivot = new Vector2(0.5f, 0f);
            windowRect.offsetMin = new Vector2(48f, 36f);
            windowRect.offsetMax = new Vector2(-48f, 36f + 300f);
            var windowImage = windowGo.GetComponent<Image>();
            windowImage.color = new Color(0.04f, 0.045f, 0.08f, 0.88f);
            windowImage.raycastTarget = false;
            _view.WindowRoot = windowGo;

            _view.PlateText = CreateText(windowRect, "Plate", 30,
                new Color(1f, 0.85f, 0.5f), TextAnchor.MiddleLeft);
            var plateRect = (RectTransform)_view.PlateText.transform;
            plateRect.anchorMin = new Vector2(0f, 1f);
            plateRect.anchorMax = new Vector2(0f, 1f);
            plateRect.pivot = new Vector2(0f, 0f);
            plateRect.anchoredPosition = new Vector2(28f, 6f);
            plateRect.sizeDelta = new Vector2(600f, 44f);

            _view.BodyText = CreateText(windowRect, "Body", 34, Color.white, TextAnchor.UpperLeft);
            var bodyRect = (RectTransform)_view.BodyText.transform;
            bodyRect.anchorMin = Vector2.zero;
            bodyRect.anchorMax = Vector2.one;
            bodyRect.offsetMin = new Vector2(32f, 24f);
            bodyRect.offsetMax = new Vector2(-32f, -24f);

            _view.PageMark = CreateText(windowRect, "PageMark", 30, Color.white, TextAnchor.LowerRight);
            _view.PageMark.text = "▼";
            var markRect = (RectTransform)_view.PageMark.transform;
            markRect.anchorMin = new Vector2(1f, 0f);
            markRect.anchorMax = new Vector2(1f, 0f);
            markRect.pivot = new Vector2(1f, 0f);
            markRect.anchoredPosition = new Vector2(-24f, 16f);
            markRect.sizeDelta = new Vector2(60f, 44f);

            // 5) 選択肢層＋雛形（並べ方はレイアウトグループに委譲）
            var choicesGo = new GameObject("Choices", typeof(RectTransform), typeof(VerticalLayoutGroup));
            choicesGo.transform.SetParent(_uiRoot.transform, false);
            var choicesRect = (RectTransform)choicesGo.transform;
            choicesRect.anchorMin = new Vector2(1f, 0f);
            choicesRect.anchorMax = new Vector2(1f, 0f);
            choicesRect.pivot = new Vector2(1f, 0f);
            choicesRect.anchoredPosition = new Vector2(-64f, 372f);
            choicesRect.sizeDelta = new Vector2(560f, 400f);
            var layout = choicesGo.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 12f;
            layout.childForceExpandHeight = false;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            _view.ChoicesRoot = choicesRect;
            _view.ChoiceTemplate = BuildChoiceTemplate(choicesRect);
            choicesGo.SetActive(false);
        }

        /// <summary>吹き出しの雛形（非アクティブ）を組む。</summary>
        private static GameObject BuildBubbleTemplate(RectTransform parent)
        {
            var root = new GameObject("BubbleTemplate", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(parent, false);
            var rect = (RectTransform)root.transform;
            rect.sizeDelta = new Vector2(560f, 150f);
            rect.pivot = new Vector2(0.5f, 0f); // 足元基準＝頭上に立つ
            var image = root.GetComponent<Image>();
            image.color = new Color(0.96f, 0.96f, 0.98f, 0.94f);
            image.raycastTarget = false; // 吹き出しはタップを遮らない

            var text = CreateText(rect, "Text", 30, new Color(0.1f, 0.1f, 0.12f), TextAnchor.UpperLeft);
            var textRect = (RectTransform)text.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(22f, 14f);
            textRect.offsetMax = new Vector2(-22f, -14f);

            root.SetActive(false);
            return root;
        }

        /// <summary>選択肢ボタンの雛形（非アクティブ）を組む。</summary>
        private static SeedButton BuildChoiceTemplate(RectTransform parent)
        {
            var buttonGo = new GameObject("ChoiceTemplate",
                typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            buttonGo.transform.SetParent(parent, false);
            buttonGo.GetComponent<LayoutElement>().preferredHeight = 72f;
            buttonGo.GetComponent<Image>().color = Color.white;

            var button = buttonGo.AddComponent<SeedButton>();
            var colors = button.colors;
            colors.normalColor = new Color(0.12f, 0.13f, 0.2f, 0.92f);
            colors.highlightedColor = new Color(0.3f, 0.33f, 0.52f, 1f);
            colors.selectedColor = new Color(0.3f, 0.33f, 0.52f, 1f);
            colors.pressedColor = new Color(0.5f, 0.54f, 0.78f, 1f);
            button.colors = colors;

            var label = CreateText((RectTransform)buttonGo.transform, "Label", 30,
                Color.white, TextAnchor.MiddleLeft);
            var labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(28f, 0f);
            labelRect.offsetMax = new Vector2(-16f, 0f);

            buttonGo.SetActive(false);
            return button;
        }

        /// <summary>選択肢ボタンを雛形から作り直す（Unity 標準のナビゲーションを縦に張る）。</summary>
        private void BuildChoices()
        {
            for (var i = _view.ChoicesRoot.childCount - 1; i >= 0; i--)
            {
                var child = _view.ChoicesRoot.GetChild(i).gameObject;
                if (child != _view.ChoiceTemplate.gameObject)
                {
                    Object.Destroy(child);
                }
            }
            _choiceButtons.Clear();

            for (var i = 0; i < _spec.ChoiceLabels.Length; i++)
            {
                var index = i; // クリック時に使う番号のキャプチャ
                var button = Object.Instantiate(_view.ChoiceTemplate, _view.ChoicesRoot);
                button.gameObject.SetActive(true);
                button.GetComponentInChildren<Text>(true).text = _spec.ChoiceLabels[i];
                _clickSubscriptions.Add(button.OnClick(() => _player?.Select(index)));
                _choiceButtons.Add(button);
            }

            // 縦のナビゲーション（端は折り返し）——移動・決定は EventSystem 標準機能が処理する
            for (var i = 0; i < _choiceButtons.Count; i++)
            {
                var navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = _choiceButtons[(i - 1 + _choiceButtons.Count) % _choiceButtons.Count],
                    selectOnDown = _choiceButtons[(i + 1) % _choiceButtons.Count],
                };
                _choiceButtons[i].navigation = navigation;
            }
        }

        /// <summary>uGUI テキストを1つ作る（サンプルは内蔵フォント）。</summary>
        private static Text CreateText(RectTransform parent, string name, int size,
            Color color, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.color = color;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>RectTransform を親いっぱいに広げる。</summary>
        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>実行中の移動・カメラ演出を畳む。</summary>
        private void CancelMotions()
        {
            for (var i = 0; i < _motions.Count; i++)
            {
                if (_motions[i].IsActive())
                {
                    _motions[i].Cancel();
                }
            }
            _motions.Clear();
        }

        /// <summary>選択肢ボタンの購読を畳む。</summary>
        private void ClearClickSubscriptions()
        {
            for (var i = 0; i < _clickSubscriptions.Count; i++)
            {
                _clickSubscriptions[i].Dispose();
            }
            _clickSubscriptions.Clear();
        }
    }
}
