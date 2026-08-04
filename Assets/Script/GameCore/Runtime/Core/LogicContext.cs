using System;
using System.Collections.Generic;

namespace Seed.Core
{
    /// <summary>
    /// ロジック1回分の実行コンテキスト。「ゲームロジック（セクション）」と
    /// 「個別仕様（イベントハンドラー）」の接点であり、コアの中心。
    ///
    /// - 時刻は long のミリ秒（float 秒の累積誤差を避ける）。ターン制では進めなくてよい
    /// - 乱数はロジック専用の決定的ストリームのみを持つ
    /// - ゲーム固有の状態（天候・記録ログ等）は「拡張」として型付きで登録する
    /// - 出力は RecordLog（事象レコード列）へ。ロジック内で文字列を組み立てない
    /// - 巻き戻しは CaptureAll / RestoreAll（推奨。コア＋購読＋参加者を一括で戻す）
    /// - CaptureCoreSnapshot / RestoreCoreSnapshot は段階移行のため従来どおり使える
    /// </summary>
    public sealed class LogicContext
    {
        /// <summary>型→ゲーム固有拡張の置き場。</summary>
        private readonly Dictionary<Type, object> _extensions = new Dictionary<Type, object>();
        /// <summary>巻き戻しに参加する状態持ち（登録順。Restore もこの順で行う）。</summary>
        private readonly List<ISnapshotParticipant> _participants = new List<ISnapshotParticipant>(4);
        /// <summary>動作設定。</summary>
        private readonly LogicContextConfig _config;
        /// <summary>セクションの入れ子深さ。</summary>
        private int _sectionDepth;
        /// <summary>今回の解決での発火回数。</summary>
        private int _eventsFiredInResolution;

        /// <summary>BeginResolution が一度でも呼ばれたか（2段プロトコルの呼び忘れ検知）。</summary>
        private bool _resolutionOpened;
        /// <summary>参加者なしの CaptureAll で毎回配列を作らないための共有の空配列。</summary>
        private static readonly object[] EmptyStates = new object[0];

        /// <summary>イベントハブ（掲示板）。</summary>
        public EventHub Hub { get; } = new EventHub();
        /// <summary>ロジック専用の決定的乱数。</summary>
        public DeterministicRandom LogicRandom { get; }

        /// <summary>現在時刻（ミリ秒）。進行役（ドライバー）が AdvanceTime で進める。</summary>
        public long NowMs { get; private set; }

        /// <summary>
        /// コア内部動作のトレース窓口（開発時専用・null なら無効でコストなし）。
        /// 再入スキップなど「黙って起きること」を観測できる。
        /// </summary>
        public ICoreTraceListener TraceListener { get; set; }

        /// <summary>LogicContext を生成する。</summary>
        public LogicContext(in LogicContextConfig config)
        {
            _config = config;
            LogicRandom = new DeterministicRandom(config.LogicSeed);
        }

        /// <summary>LogicContext を生成する。</summary>
        public LogicContext(uint logicSeed) : this(new LogicContextConfig(logicSeed))
        {
        }

        // ---------------- 時間 ----------------

        /// <summary>時間を進める（リアルタイム制では deltaTime 累積、ターン制では使わなくてよい）。</summary>
        public void AdvanceTime(long deltaMs)
        {
            if (deltaMs < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaMs));
            }
            NowMs += deltaMs;
        }

        // ---------------- スナップショット（巻き戻し・先読みAI） ----------------

        /// <summary>
        /// コア側の可変状態（時刻・乱数状態・暴走ガードのカウンタ）を保存する。
        /// あわせてゲーム側で「アクター状態のコピー」と「RecordLog.Count の控え」を取ること
        /// （呼び漏れを避けたいなら CaptureAll を使う）。
        /// </summary>
        public CoreSnapshot CaptureCoreSnapshot()
        {
            return new CoreSnapshot(NowMs, LogicRandom.CaptureState(),
                _eventsFiredInResolution, _sectionDepth);
        }

        /// <summary>
        /// コア側の可変状態を復元する。CaptureCoreSnapshot とペアで使う。
        ///
        /// 発火数・セクション深度は「解決の途中で試し実行→巻き戻し」をしても
        /// 暴走ガードが誤発火しないように戻す（旧2引数コンストラクタ製の値では戻さない）。
        /// 通常は Capture と同じ入れ子位置で Restore するため深度は同値＝実質無変化。
        /// </summary>
        public void RestoreCoreSnapshot(in CoreSnapshot snapshot)
        {
            NowMs = snapshot.NowMs;
            LogicRandom.RestoreState(snapshot.RngState);
            if (snapshot.HasExtendedState)
            {
                _eventsFiredInResolution = snapshot.EventsFiredInResolution;
                _sectionDepth = snapshot.SectionDepth;
            }
        }

        // ---------------- 統合スナップショット ----------------

        /// <summary>
        /// 巻き戻しに参加する状態持ちを手動で登録する（拡張として登録しないものはこちら）。
        /// 登録順は保持され、RestoreAll もこの順で復元する。同一インスタンスの二重登録は無視する
        /// （復元が2回走るだけで害はないが、無駄なので弾く）。
        /// </summary>
        public void AddSnapshotParticipant(ISnapshotParticipant participant)
        {
            if (participant == null)
            {
                throw new ArgumentNullException(nameof(participant));
            }
            for (var i = 0; i < _participants.Count; i++)
            {
                if (ReferenceEquals(_participants[i], participant))
                {
                    return;
                }
            }
            _participants.Add(participant);
        }

        /// <summary>登録済みの参加者数（診断・アサート用）。</summary>
        public int SnapshotParticipantCount => _participants.Count;

        /// <summary>
        /// 世界の状態を一括で保存する（コア＋購読＋全参加者）。
        /// 「どれか1系統を忘れて稀に結果がズレる」事故を構造的に防ぐための推奨経路。
        /// EventHub の購読を複製するためアロケーションを伴う＝節目で使うこと。
        /// </summary>
        public WorldSnapshot CaptureAll()
        {
            var states = _participants.Count == 0
                ? EmptyStates
                : new object[_participants.Count];
            for (var i = 0; i < _participants.Count; i++)
            {
                states[i] = _participants[i].CaptureState();
            }
            return new WorldSnapshot(CaptureCoreSnapshot(), Hub.CaptureSnapshot(), states);
        }

        /// <summary>
        /// CaptureAll で保存した状態へ一括で戻す。
        /// 参加者の構成（登録順・件数）が Capture 時と違う場合は、
        /// 対応関係が崩れて静かに壊れるため構成ミスとして例外にする。
        /// </summary>
        public void RestoreAll(WorldSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }
            if (snapshot.ParticipantCount != _participants.Count)
            {
                throw new LogicException(
                    $"スナップショット参加者の件数が変化している（保存時: {snapshot.ParticipantCount} / 現在: {_participants.Count}）");
            }

            RestoreCoreSnapshot(in snapshot.Core);
            Hub.RestoreSnapshot(snapshot.Subscriptions);
            for (var i = 0; i < _participants.Count; i++)
            {
                _participants[i].RestoreState(snapshot.ParticipantStates[i]);
            }
        }

        // ---------------- 拡張（ゲーム固有状態） ----------------

        /// <summary>
        /// ゲーム固有の状態（RecordLog・天候・フィールド等）を型をキーに登録する。
        /// コアを非ジェネリックに保ちながら、ゲームごとの状態を型安全に持たせるための仕組み。
        ///
        /// 拡張が ISnapshotParticipant を実装していれば巻き戻しの参加者として自動登録する
        /// （合成ルートで「参加者登録も書く」二重の手間＝忘れどころを作らないため）。
        /// </summary>
        public void AddExtension<T>(T extension) where T : class
        {
            if (extension == null)
            {
                throw new ArgumentNullException(nameof(extension));
            }
            _extensions.Add(typeof(T), extension);
            if (extension is ISnapshotParticipant participant)
            {
                AddSnapshotParticipant(participant);
            }
        }

        /// <summary>登録済み拡張の取得。未登録なら LogicException（合成ルートの構成ミスを早期検知）。</summary>
        public T GetExtension<T>() where T : class
        {
            if (_extensions.TryGetValue(typeof(T), out var value))
            {
                return (T)value;
            }
            throw new LogicException($"拡張 {typeof(T).Name} が登録されていない（合成ルートを確認）");
        }

        /// <summary>拡張の取得を試みる。</summary>
        public bool TryGetExtension<T>(out T extension) where T : class
        {
            if (_extensions.TryGetValue(typeof(T), out var value))
            {
                extension = (T)value;
                return true;
            }
            extension = null;
            return false;
        }

        // ---------------- 解決の境界 ----------------

        /// <summary>
        /// 1回の「解決」（攻撃1回・アイテム使用1回など）の開始宣言。
        /// イベント発火数カウンタをリセットする。進行役の入口で必ず呼ぶこと。
        /// </summary>
        public void BeginResolution()
        {
            _eventsFiredInResolution = 0;
            _resolutionOpened = true;
        }

        /// <summary>発火回数を数え、上限超過は暴走として例外を投げる。</summary>
        internal void CountEventFired()
        {
            _eventsFiredInResolution++;
            if (_eventsFiredInResolution > _config.MaxEventsPerResolution)
            {
                throw new LogicException(
                    $"1解決あたりのイベント発火数が上限({_config.MaxEventsPerResolution})を超えた（連鎖の暴走を検知）");
            }
        }

        // ---------------- セクション実行 ----------------

        /// <summary>
        /// セクションを実行する。イベントハンドラーからも呼び出せるのが要点で、
        /// これにより「割り込み」「連鎖」をメインロジック無変更で実現する。
        /// 深度上限を超えた場合は設計バグとして例外を投げる。
        /// </summary>
        public TResult RunSection<TInput, TResult>(Section<TInput, TResult> section, in TInput input)
        {
            // 2段プロトコル（BeginResolution → RunSection）の呼び忘れをトップレベルで検知する。
            // 1回の Begin に複数の RunSection は正当（発動判定→ヒット解決）なので、
            // 検知できるのは「一度も Begin していない」進行役の書き漏らしだけ——それが最頻の事故。
            if (_sectionDepth == 0 && !_resolutionOpened)
            {
                throw new LogicException(
                    $"BeginResolution を呼ばずにセクションを実行した: {section.Name}（進行役の入口で必ず宣言する）");
            }
            if (_sectionDepth >= _config.MaxSectionDepth)
            {
                throw new LogicException(
                    $"セクション深度が上限({_config.MaxSectionDepth})を超えた: {section.Name}");
            }

            TraceListener?.OnSectionEnter(section.Name, _sectionDepth);
            _sectionDepth++;
            try
            {
                return section.Execute(this, in input);
            }
            finally
            {
                _sectionDepth--;
                TraceListener?.OnSectionExit(section.Name, _sectionDepth);
            }
        }
    }
}
