using System;
using System.Collections.Generic;

namespace Seed.Adv
{
    /// <summary>ADV進行の段階。</summary>
    public enum AdvState
    {
        /// <summary>文字送り中（送り操作で全文表示へ）。</summary>
        Typing,

        /// <summary>ページ全文表示済み（送り操作で次ページ or 選択肢 or 終了へ）。</summary>
        PageComplete,

        /// <summary>選択肢の提示中（Select で確定。選択カーソルはUI側の持ち物）。</summary>
        Choosing,

        /// <summary>終了（ResultId が確定）。</summary>
        Finished,
    }

    /// <summary>
    /// ADV進行の状態機械（純C#。文字送り・ページ送り・選択肢の確定）。
    ///
    /// [役割分担] 本クラスは「今どのページの何文字目まで見せてよいか」「次の操作で
    /// 何が起きるか」という**進行の真実**だけを持つ。表示（uGUIテキスト・ボタン）と
    /// 入力（タップ・EventSystem のナビゲーション）はフェーズ/UI側の仕事で、
    /// 選択肢のカーソル位置も UI（EventSystem）の持ち物——真実を二重に持たない。
    ///
    /// [操作の契約]
    /// - <see cref="Advance"/> … 「送り」1回ぶん。文字送り中=全文表示 /
    ///   全文表示済み=次ページ（最終ページなら選択肢へ、選択肢が無ければ終了）
    /// - <see cref="Select"/> … 選択肢の確定（UIのボタン押下から呼ぶ）
    ///
    /// 純C#・決定的（同じ dt 列と操作列 → 同じ進行）なので EditMode で検証できる。
    /// </summary>
    public sealed class AdvPlayer
    {
        /// <summary>台本。</summary>
        private readonly AdvScript _script;

        /// <summary>文字送り速度（文字/秒。0以下=瞬間表示）。</summary>
        private readonly float _charsPerSecond;

        /// <summary>文字送りの累積（小数を保持して離散の文字数へ丸める）。</summary>
        private float _revealAccumulator;

        /// <summary>AdvPlayer を生成する（生成直後は1ページ目の文字送りから始まる）。</summary>
        public AdvPlayer(AdvScript script, float charsPerSecond = 30f)
        {
            _script = script ?? throw new ArgumentNullException(nameof(script));
            _charsPerSecond = charsPerSecond;
            EnterPage(0);
        }

        /// <summary>現在の段階。</summary>
        public AdvState State { get; private set; } = AdvState.Typing;

        /// <summary>現在のページ番号（0始まり）。</summary>
        public int PageIndex { get; private set; }

        /// <summary>現在のページ（本文・話者・見せ方・演技。演技の実行は表示側の仕事）。</summary>
        public AdvPage CurrentPage => _script.Pages[PageIndex];

        /// <summary>現在のページの全文。</summary>
        public string PageText => CurrentPage.Text;

        /// <summary>今見せてよい文字数（表示側は先頭からこの文字数だけ描く）。</summary>
        public int VisibleLength { get; private set; }

        /// <summary>最終ページか。</summary>
        public bool IsLastPage => PageIndex == _script.Pages.Count - 1;

        /// <summary>選択肢（空=強制イベント）。</summary>
        public IReadOnlyList<AdvChoice> Choices => _script.Choices;

        /// <summary>終了時の結果ID（選択肢なしの終了は 0）。</summary>
        public int ResultId { get; private set; }

        /// <summary>ページが切り替わった（表示のリセット用）。</summary>
        public event Action PageChanged;

        /// <summary>段階が変わった（▼表示・選択肢の出し入れ用）。</summary>
        public event Action StateChanged;

        /// <summary>終了した（引数=ResultId。次のイベントへの分岐はアプリが決める）。</summary>
        public event Action<int> Finished;

        /// <summary>文字送りを進める（毎Tick呼ぶ。文字送り中以外は何もしない）。</summary>
        public void Tick(float deltaTime)
        {
            if (State != AdvState.Typing || deltaTime <= 0f)
            {
                return;
            }
            if (_charsPerSecond <= 0f)
            {
                RevealAll(); // 0以下=瞬間表示の仕様（演出を切りたいデータ向け）
                return;
            }
            _revealAccumulator += deltaTime * _charsPerSecond;
            var visible = (int)_revealAccumulator;
            if (visible >= PageText.Length)
            {
                RevealAll();
                return;
            }
            VisibleLength = visible;
        }

        /// <summary>「送り」1回ぶん（タップ・決定ボタンから呼ぶ）。</summary>
        public void Advance()
        {
            switch (State)
            {
                case AdvState.Typing:
                    RevealAll(); // 文字送り中の送りは「一括で全文表示」
                    break;
                case AdvState.PageComplete:
                    if (!IsLastPage)
                    {
                        EnterPage(PageIndex + 1);
                        PageChanged?.Invoke();
                        StateChanged?.Invoke(); // PageComplete→Typing（▼を消す）
                    }
                    else if (_script.Choices.Count > 0)
                    {
                        SetState(AdvState.Choosing);
                    }
                    else
                    {
                        Finish(0); // 選択肢なし＝強制イベントの終了
                    }
                    break;
                // Choosing / Finished では何もしない（確定は Select の仕事）
            }
        }

        /// <summary>選択肢を確定する（選択中以外・範囲外は無視）。</summary>
        public void Select(int index)
        {
            if (State != AdvState.Choosing || index < 0 || index >= _script.Choices.Count)
            {
                return;
            }
            Finish(_script.Choices[index].ResultId);
        }

        /// <summary>ページを最初から始める。</summary>
        private void EnterPage(int index)
        {
            PageIndex = index;
            VisibleLength = 0;
            _revealAccumulator = 0f;
            State = AdvState.Typing;
        }

        /// <summary>全文表示にして「送り待ち」へ。</summary>
        private void RevealAll()
        {
            VisibleLength = PageText.Length;
            SetState(AdvState.PageComplete);
        }

        /// <summary>段階を変えて通知する（同じ段階なら何もしない）。</summary>
        private void SetState(AdvState state)
        {
            if (State == state)
            {
                return;
            }
            State = state;
            StateChanged?.Invoke();
        }

        /// <summary>結果を確定して終了する。</summary>
        private void Finish(int resultId)
        {
            ResultId = resultId;
            SetState(AdvState.Finished);
            Finished?.Invoke(resultId);
        }
    }
}
