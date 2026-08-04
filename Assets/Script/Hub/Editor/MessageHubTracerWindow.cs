using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Seed.Hub.Editor
{
    /// <summary>
    /// メッセージフローの観測ウィンドウ（Seed/Message Tracer）。
    ///
    /// 「基盤同士が互いを知らない」アーキテクチャでは、不具合調査の唯一の手掛かりが
    /// メッセージフロー——本ウィンドウは MessageHub の観測フック
    /// （MessagePublished / DeliveryFailed）を購読し、
    /// 直近の発行履歴（リングバッファ）と型別頻度を一覧する。
    ///
    /// - Hub の発見は MessageHubRegistry（開発ビルド限定の弱参照台帳）経由。
    ///   Play 中に生成されたHubも毎秒の再走査で自動的に拾う
    /// - 記録するのは「型名・購読者数・時刻・失敗」だけ（本体は渡ってこない＝ボックス化なし）
    /// </summary>
    public sealed class MessageHubTracerWindow : EditorWindow
    {
        /// <summary>履歴1件。</summary>
        private readonly struct Entry
        {
            /// <summary>記録時刻（起動からの秒）。</summary>
            public readonly double Time;

            /// <summary>発行元Hubの番号。</summary>
            public readonly int HubIndex;

            /// <summary>メッセージ型名。</summary>
            public readonly string TypeName;

            /// <summary>配達先の購読者数。</summary>
            public readonly int Subscribers;

            /// <summary>配達中の例外（無ければ null）。</summary>
            public readonly string Error;

            /// <summary>Entry を生成する。</summary>
            public Entry(double time, int hubIndex, string typeName, int subscribers, string error)
            {
                Time = time;
                HubIndex = hubIndex;
                TypeName = typeName;
                Subscribers = subscribers;
                Error = error;
            }
        }

        /// <summary>履歴の最大件数（リングバッファ）。</summary>
        private const int MaxEntries = 256;

        /// <summary>購読済みのHubと解除用デリゲート。</summary>
        private readonly List<(MessageHub Hub, Action<Type, int> OnPublished, Action<Type, Exception> OnFailed)>
            _attached = new List<(MessageHub, Action<Type, int>, Action<Type, Exception>)>();

        /// <summary>Hub再走査用のバッファ。</summary>
        private readonly List<MessageHub> _aliveBuffer = new List<MessageHub>(4);

        /// <summary>発行履歴。</summary>
        private readonly List<Entry> _entries = new List<Entry>(MaxEntries);

        /// <summary>型別の発行回数。</summary>
        private readonly Dictionary<string, int> _frequency = new Dictionary<string, int>();

        /// <summary>頻度表示のソート用。</summary>
        private readonly List<KeyValuePair<string, int>> _frequencySorted =
            new List<KeyValuePair<string, int>>();

        /// <summary>記録中か。</summary>
        private bool _isCapturing = true;

        /// <summary>頻度サマリを表示するか。</summary>
        private bool _showFrequency = true;

        /// <summary>履歴のスクロール位置。</summary>
        private Vector2 _scroll;

        /// <summary>次にHubを再走査する時刻。</summary>
        private double _nextRescan;

        /// <summary>ウィンドウを開く。</summary>
        [MenuItem("Seed/Message Tracer")]
        public static void Open()
        {
            var window = GetWindow<MessageHubTracerWindow>("Message Tracer");
            window.minSize = new Vector2(420f, 240f);
        }

        /// <summary>購読を開始する。</summary>
        private void OnEnable()
        {
            _nextRescan = 0;
            EditorApplication.update += OnEditorUpdate;
        }

        /// <summary>購読を解除する。</summary>
        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            DetachAll();
        }

        /// <summary>毎秒Hubを再走査し、記録中は表示を更新する。</summary>
        private void OnEditorUpdate()
        {
            if (EditorApplication.timeSinceStartup >= _nextRescan)
            {
                _nextRescan = EditorApplication.timeSinceStartup + 1.0;
                RescanHubs();
            }
            if (_isCapturing && _entries.Count > 0)
            {
                Repaint();
            }
        }

        /// <summary>台帳から生きているHubを拾い、未購読のものへフックを張る。</summary>
        private void RescanHubs()
        {
            MessageHubRegistry.CollectAlive(_aliveBuffer);

            // 死んだHubの購読を整理
            for (var i = _attached.Count - 1; i >= 0; i--)
            {
                if (!_aliveBuffer.Contains(_attached[i].Hub))
                {
                    _attached.RemoveAt(i);
                }
            }

            for (var i = 0; i < _aliveBuffer.Count; i++)
            {
                var hub = _aliveBuffer[i];
                var known = false;
                for (var k = 0; k < _attached.Count; k++)
                {
                    if (ReferenceEquals(_attached[k].Hub, hub))
                    {
                        known = true;
                        break;
                    }
                }
                if (known)
                {
                    continue;
                }

                var hubIndex = _attached.Count;
                Action<Type, int> onPublished = (type, subscribers) =>
                    Record(hubIndex, type, subscribers, null);
                Action<Type, Exception> onFailed = (type, exception) =>
                    Record(hubIndex, type, -1, exception.GetType().Name + ": " + exception.Message);
                hub.MessagePublished += onPublished;
                hub.DeliveryFailed += onFailed;
                _attached.Add((hub, onPublished, onFailed));
            }
        }

        /// <summary>全Hubからフックを外す。</summary>
        private void DetachAll()
        {
            for (var i = 0; i < _attached.Count; i++)
            {
                _attached[i].Hub.MessagePublished -= _attached[i].OnPublished;
                _attached[i].Hub.DeliveryFailed -= _attached[i].OnFailed;
            }
            _attached.Clear();
        }

        /// <summary>1件記録する（リングバッファ・頻度更新）。</summary>
        private void Record(int hubIndex, Type type, int subscribers, string error)
        {
            if (!_isCapturing)
            {
                return;
            }
            if (_entries.Count >= MaxEntries)
            {
                _entries.RemoveAt(0);
            }
            _entries.Add(new Entry(
                EditorApplication.timeSinceStartup, hubIndex, type.Name, subscribers, error));
            _frequency.TryGetValue(type.Name, out var count);
            _frequency[type.Name] = count + 1;
        }

        /// <summary>ツールバー・頻度サマリ・履歴を描画する。</summary>
        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                _isCapturing = GUILayout.Toggle(_isCapturing, "記録", EditorStyles.toolbarButton, GUILayout.Width(50f));
                _showFrequency = GUILayout.Toggle(_showFrequency, "型別頻度", EditorStyles.toolbarButton, GUILayout.Width(70f));
                if (GUILayout.Button("クリア", EditorStyles.toolbarButton, GUILayout.Width(50f)))
                {
                    _entries.Clear();
                    _frequency.Clear();
                }
                GUILayout.FlexibleSpace();
                GUILayout.Label($"Hub: {_attached.Count}  記録: {_entries.Count}/{MaxEntries}", EditorStyles.miniLabel);
            }

            if (_attached.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "観測中の MessageHub がありません。Play を開始すると自動で接続されます。", MessageType.Info);
            }

            if (_showFrequency && _frequency.Count > 0)
            {
                _frequencySorted.Clear();
                _frequencySorted.AddRange(_frequency);
                _frequencySorted.Sort((a, b) => b.Value.CompareTo(a.Value));
                var summary = "";
                for (var i = 0; i < _frequencySorted.Count && i < 8; i++)
                {
                    summary += $"{_frequencySorted[i].Key} ×{_frequencySorted[i].Value}   ";
                }
                EditorGUILayout.LabelField(summary, EditorStyles.miniLabel);
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (var i = _entries.Count - 1; i >= 0; i--)
            {
                var entry = _entries[i];
                var line = entry.Error == null
                    ? $"[{entry.Time:0.00}s] Hub{entry.HubIndex}  {entry.TypeName}  → {entry.Subscribers}件配達"
                    : $"[{entry.Time:0.00}s] Hub{entry.HubIndex}  {entry.TypeName}  ✕ {entry.Error}";
                var style = entry.Error == null ? EditorStyles.label : EditorStyles.boldLabel;
                EditorGUILayout.LabelField(line, style);
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
