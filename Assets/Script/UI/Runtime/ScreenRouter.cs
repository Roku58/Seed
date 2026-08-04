using System.Collections.Generic;
using Seed.Hub;
using Seed.Hub.Contracts;

namespace Seed.UI
{
    /// <summary>
    /// 画面の交通整理（登録・レイヤ別の切替・戻りスタック・出入り演出の駆動）。
    /// プレハブのロード戦略は持たない——「登録済みインスタンスの切替」だけに絞り、
    /// ロードが必要になったら外側で読み込んでから Register する。
    ///
    /// レイヤ構造:
    /// - Base    … 同時に1枚（HUD・タイトル・リザルト）。履歴はこのレイヤだけが持つ
    /// - Overlay … Base の上の常設物。複数同時表示できる
    /// - Modal   … 入力を奪う前面。後入れ先出しのスタック（ポーズ→確認、の重なり）
    ///
    /// 遷移モード（ScreenTransition）:
    /// - Push         … 現画面を履歴へ積む（Back で戻れる）
    /// - Replace      … 履歴に積まず置き換える
    /// - ClearAndShow … 履歴を全消去して表示（タイトルへ戻る等「戻ってはいけない遷移」）
    ///
    /// 出入り演出（IScreenTransition）:
    /// 画面が CreateShow/HideTransition で返せば、Tick(dt) が完了まで駆動する
    /// （抜け演出中の画面は表示されたまま残り、完了時に消える＝クロスフェード）。
    /// 状態変更（OnShow/OnHide）は演出を待たず即時——演出は艶であり真実に関与しない。
    /// </summary>
    public sealed class ScreenRouter
    {
        /// <summary>戻り履歴の上限（超過時は最古を捨てる。往復で無限に伸びるのを防ぐ）。</summary>
        public const int MaxHistory = 32;

        /// <summary>進行中の演出1件。</summary>
        private readonly struct RunningTransition
        {
            /// <summary>演出中の画面。</summary>
            public readonly UIScreen Screen;

            /// <summary>演出本体。</summary>
            public readonly IScreenTransition Transition;

            /// <summary>完了時に表示を消すか（抜け演出のみ true）。</summary>
            public readonly bool DeactivateOnDone;

            /// <summary>RunningTransition を生成する。</summary>
            public RunningTransition(UIScreen screen, IScreenTransition transition, bool deactivateOnDone)
            {
                Screen = screen;
                Transition = transition;
                DeactivateOnDone = deactivateOnDone;
            }
        }

        /// <summary>登録済みの画面。</summary>
        private readonly Dictionary<ScreenId, UIScreen> _screens = new Dictionary<ScreenId, UIScreen>();

        /// <summary>Base レイヤの戻り履歴（List で持ち、上限超過時に先頭を捨てる）。</summary>
        private readonly List<ScreenId> _history = new List<ScreenId>(8);

        /// <summary>表示中の Overlay。</summary>
        private readonly List<ScreenId> _overlays = new List<ScreenId>(2);

        /// <summary>表示中の Modal（後入れ先出し）。</summary>
        private readonly List<ScreenId> _modals = new List<ScreenId>(2);

        /// <summary>進行中の演出。</summary>
        private readonly List<RunningTransition> _running = new List<RunningTransition>(2);

        /// <summary>現在の Base 画面（無ければ None）。</summary>
        public ScreenId Current { get; private set; } = ScreenId.None;

        /// <summary>表示中の Modal 数（入力ロック判定に使える）。</summary>
        public int ModalCount => _modals.Count;

        /// <summary>戻り履歴の件数。</summary>
        public int HistoryCount => _history.Count;

        /// <summary>出入り演出が進行中か（アプリの入力ロック判断用）。</summary>
        public bool IsTransitionRunning => _running.Count > 0;

        /// <summary>画面を登録し、初期状態として非表示にする。二重登録は構成ミスとして例外。</summary>
        public void Register(UIScreen screen)
        {
            if (_screens.ContainsKey(screen.Id))
            {
                throw new HubException($"{screen.Id} は登録済み（画面IDの重複）");
            }
            _screens.Add(screen.Id, screen);
            screen.gameObject.SetActive(false);
        }

        /// <summary>
        /// 指定画面を表示する。レイヤは画面自身の宣言（UIScreen.Layer）に従う。
        /// 未登録IDは打ち間違いとして例外。
        /// </summary>
        public void Show(ScreenId id, ScreenTransition transition = ScreenTransition.Push, int payload = 0)
        {
            if (!_screens.TryGetValue(id, out var next))
            {
                throw new HubException($"{id} は未登録の画面（Register漏れかIDの打ち間違い）");
            }

            switch (next.Layer)
            {
                case ScreenLayer.Base:
                    ShowBase(id, next, transition, payload);
                    break;
                case ScreenLayer.Overlay:
                    if (!_overlays.Contains(id))
                    {
                        _overlays.Add(id);
                        BeginShow(next, payload);
                    }
                    break;
                case ScreenLayer.Modal:
                    if (!_modals.Contains(id))
                    {
                        _modals.Add(id);
                        BeginShow(next, payload);
                    }
                    break;
            }
        }

        /// <summary>最前面の Modal（または指定 Overlay 全部）を閉じる。閉じたら true。</summary>
        public bool Close(ScreenLayer layer)
        {
            switch (layer)
            {
                case ScreenLayer.Modal:
                    if (_modals.Count == 0)
                    {
                        return false;
                    }
                    var top = _modals[_modals.Count - 1];
                    _modals.RemoveAt(_modals.Count - 1);
                    BeginHide(_screens[top]);
                    return true;

                case ScreenLayer.Overlay:
                    if (_overlays.Count == 0)
                    {
                        return false;
                    }
                    for (var i = 0; i < _overlays.Count; i++)
                    {
                        BeginHide(_screens[_overlays[i]]);
                    }
                    _overlays.Clear();
                    return true;

                default:
                    return false; // Base は Close ではなく Back / Show で遷移する
            }
        }

        /// <summary>1つ前の Base 画面へ戻る。履歴が無ければ false。</summary>
        public bool Back()
        {
            if (_history.Count == 0)
            {
                return false;
            }

            if (_screens.TryGetValue(Current, out var current))
            {
                BeginHide(current);
            }
            var last = _history[_history.Count - 1];
            _history.RemoveAt(_history.Count - 1);
            Current = last;
            BeginShow(_screens[Current], 0);
            return true;
        }

        /// <summary>進行中の出入り演出を進める（UISystem.Tick 経由で毎フレーム呼ばれる）。</summary>
        public void Tick(float deltaTime)
        {
            for (var i = _running.Count - 1; i >= 0; i--)
            {
                var running = _running[i];
                running.Transition.Tick(deltaTime);
                if (!running.Transition.IsDone)
                {
                    continue;
                }
                if (running.DeactivateOnDone)
                {
                    running.Screen.Deactivate();
                }
                _running.RemoveAt(i);
            }
        }

        /// <summary>Base レイヤの遷移を実行する。</summary>
        private void ShowBase(ScreenId id, UIScreen next, ScreenTransition transition, int payload)
        {
            if (Current.Equals(id))
            {
                return;
            }

            if (transition == ScreenTransition.ClearAndShow)
            {
                _history.Clear();
            }

            if (_screens.TryGetValue(Current, out var current))
            {
                BeginHide(current);
                if (transition == ScreenTransition.Push)
                {
                    _history.Add(Current);
                    if (_history.Count > MaxHistory)
                    {
                        _history.RemoveAt(0); // 最古を捨てる（往復の無限成長防止）
                    }
                }
            }
            Current = id;
            BeginShow(next, payload);
        }

        /// <summary>入り演出つきで表示を始める（進行中の演出があれば先に終端へ飛ばす）。</summary>
        private void BeginShow(UIScreen screen, int payload)
        {
            CompleteRunningFor(screen);
            var transition = screen.BeginShow(payload);
            if (transition != null && !transition.IsDone)
            {
                _running.Add(new RunningTransition(screen, transition, deactivateOnDone: false));
            }
        }

        /// <summary>抜け演出つきで非表示を始める（演出が無ければ即時に消える）。</summary>
        private void BeginHide(UIScreen screen)
        {
            CompleteRunningFor(screen);
            var transition = screen.BeginHide();
            if (transition == null || transition.IsDone)
            {
                screen.Deactivate();
                return;
            }
            _running.Add(new RunningTransition(screen, transition, deactivateOnDone: true));
        }

        /// <summary>同一画面の進行中演出を終端へ飛ばす（出入りの割り込みで状態が濁らないように）。</summary>
        private void CompleteRunningFor(UIScreen screen)
        {
            for (var i = _running.Count - 1; i >= 0; i--)
            {
                if (!ReferenceEquals(_running[i].Screen, screen))
                {
                    continue;
                }
                var running = _running[i];
                running.Transition.Complete();
                if (running.DeactivateOnDone)
                {
                    running.Screen.Deactivate();
                }
                _running.RemoveAt(i);
            }
        }
    }
}
