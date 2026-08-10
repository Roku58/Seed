using System;
using System.Collections.Generic;

namespace Seed.Adv
{
    /// <summary>
    /// 選択肢1件（表示ラベルと、選んだときの結果ID）。
    /// 結果IDの意味づけ（次のイベントID・購入する商品ID など）はアプリが決める
    /// ——基盤は番号を運ぶだけ（IDはint値型の家風と同じ分業）。
    /// </summary>
    public sealed class AdvChoice
    {
        /// <summary>ボタンに出す表示ラベル。</summary>
        public string Label { get; }

        /// <summary>選んだときの結果ID（意味づけはアプリ）。</summary>
        public int ResultId { get; }

        /// <summary>AdvChoice を生成する。</summary>
        public AdvChoice(string label, int resultId)
        {
            Label = label ?? string.Empty;
            ResultId = resultId;
        }
    }

    /// <summary>ページ本文の見せ方。</summary>
    public enum AdvSpeechStyle
    {
        /// <summary>画面下部の固定テキストボックスに表示。</summary>
        Window = 0,

        /// <summary>話者の吹き出しに表示（静的な吹き出しの演技と合わせて0〜複数を同時に出せる）。</summary>
        Bubble = 1,
    }

    /// <summary>演技（舞台指示）の種類。</summary>
    public enum AdvActKind
    {
        /// <summary>移動（X/Y/Z の位置へ Seconds かけて。0=瞬間）。</summary>
        Move = 0,

        /// <summary>モーション再生（Key=ステート名。再生方法は表示側の持ち物）。</summary>
        Motion = 1,

        /// <summary>モデルの読み替え（Key=プレハブキー / SubKey=コントローラキー）。</summary>
        Model = 2,

        /// <summary>登場（表示ON）。</summary>
        Show = 3,

        /// <summary>退場（表示OFF）。</summary>
        Hide = 4,

        /// <summary>
        /// 静的な吹き出しを出す（Key=本文 / X,Y,Z=位置オフセット（全て0なら既定値）/
        /// Seconds=表示時間（0=消去指示まで残す））。話者の吹き出しとは独立に
        /// 何枚でも重ねられる＝吹き出しは0〜複数を同時に表示できる。
        /// </summary>
        Bubble = 5,

        /// <summary>静的な吹き出しを消す（ActorId=対象キャラ。0なら全キャラぶん）。</summary>
        BubbleClear = 6,

        /// <summary>
        /// タイムライン演出を再生する（Key=PlayableDirector 入りプレハブのキー。
        /// カット演出などアセットで作った演出をデータから起動する口）。
        /// </summary>
        Timeline = 7,

        /// <summary>
        /// カメラを動かす（X,Y,Z=カメラ位置 / ActorId=注視するキャラ（0=正面のまま）/
        /// Seconds=移動時間（0=瞬間））。会話の寄り・引きをデータで作る。
        /// 2D舞台（立ち絵）では意味を持たないため表示側が無視してよい。
        /// </summary>
        Camera = 8,

        /// <summary>
        /// タイムライン演出を止めて片付ける（Key=止める対象のプレハブキー。空=全部）。
        /// Timeline の強制クローズ命令。
        /// </summary>
        TimelineStop = 9,

        /// <summary>
        /// カメラを既定の位置へ戻す（Seconds=戻りの補間時間。0=瞬間）。
        /// Camera / CameraSwitch の強制クローズ命令。
        /// </summary>
        CameraReset = 10,

        /// <summary>
        /// コード側へ合図を飛ばす（Key=合図名。表示側が通知として発行し、
        /// 購読しているコードが任意の処理——報酬付与・フラグ更新など——を行う）。
        /// </summary>
        Signal = 11,

        /// <summary>
        /// 事前定義したカメラへ切り替える（ActorId=切替先カメラID /
        /// Seconds=ブレンド時間）。複数カメラの構図をデータで行き来する。
        /// </summary>
        CameraSwitch = 12,
    }

    /// <summary>
    /// 演出の寿命（いつ自動で閉じるか）。時間による寿命は各演技の Seconds が担い、
    /// こちらは「進行のきっかけ」による寿命を表す——両方指定した場合は早い方で閉じる。
    /// 明示の閉じ命令（BubbleClear / TimelineStop / CameraReset）はいつでも使える。
    /// </summary>
    public enum AdvActLife
    {
        /// <summary>閉じ命令（または時間指定）まで残す。</summary>
        Keep = 0,

        /// <summary>ページが切り替わったら閉じる。</summary>
        Page = 1,

        /// <summary>次の送り操作（タップ・決定）で閉じる。</summary>
        Advance = 2,
    }

    /// <summary>
    /// 演技1件（ページ開始時に実行される舞台指示）。
    ///
    /// 純データであり、実行（Transform の移動・アニメ再生・プレハブ差し替え）は
    /// 表示側の仕事——「会話の進行に合わせて何をするか」だけを台本が持つ。
    /// タイムラインアセットで演出したい場合も、それを起動する指示を
    /// この形（Kind の追加）で台本に載せる拡張ができる。
    /// </summary>
    public sealed class AdvAct
    {
        /// <summary>対象キャラのID（意味づけはアプリの登場キャラ表）。</summary>
        public int ActorId { get; }

        /// <summary>指示の種類。</summary>
        public AdvActKind Kind { get; }

        /// <summary>文字列パラメータ（Motion=ステート名 / Model=プレハブキー）。</summary>
        public string Key { get; }

        /// <summary>補助の文字列パラメータ（Model=コントローラキー）。</summary>
        public string SubKey { get; }

        /// <summary>位置パラメータ X（Move で使用）。</summary>
        public float X { get; }

        /// <summary>位置パラメータ Y（Move で使用）。</summary>
        public float Y { get; }

        /// <summary>位置パラメータ Z（Move で使用）。</summary>
        public float Z { get; }

        /// <summary>所要秒数（Move/Camera=補間時間 / Bubble/Timeline=時間寿命。0=なし）。</summary>
        public float Seconds { get; }

        /// <summary>寿命（Bubble/Timeline/Camera が対象。ページ切替・送り操作で自動で閉じる）。</summary>
        public AdvActLife Life { get; }

        /// <summary>AdvAct を生成する。</summary>
        public AdvAct(int actorId, AdvActKind kind, string key = "", string subKey = "",
            float x = 0f, float y = 0f, float z = 0f, float seconds = 0f,
            AdvActLife life = AdvActLife.Keep)
        {
            ActorId = actorId;
            Kind = kind;
            Key = key ?? string.Empty;
            SubKey = subKey ?? string.Empty;
            X = x;
            Y = y;
            Z = z;
            Seconds = seconds;
            Life = life;
        }
    }

    /// <summary>
    /// 1ページぶんの台本（本文＋話者＋見せ方＋ページ開始時の演技）。
    /// 話者ID 0 はナレーション（話者なし）の約束。
    /// </summary>
    public sealed class AdvPage
    {
        /// <summary>演技なしを表す共有インスタンス。</summary>
        private static readonly AdvAct[] NoActs = new AdvAct[0];

        /// <summary>本文（文字送りで表示される）。</summary>
        public string Text { get; }

        /// <summary>話者のキャラID（0=ナレーション）。</summary>
        public int SpeakerId { get; }

        /// <summary>見せ方（固定テキストボックス / 吹き出し）。</summary>
        public AdvSpeechStyle Style { get; }

        /// <summary>このページに入った瞬間に実行する演技。</summary>
        public IReadOnlyList<AdvAct> Acts { get; }

        /// <summary>AdvPage を生成する（本文 null はデータ不備として即例外）。</summary>
        public AdvPage(string text, int speakerId = 0,
            AdvSpeechStyle style = AdvSpeechStyle.Window, IReadOnlyList<AdvAct> acts = null)
        {
            Text = text ?? throw new ArgumentNullException(nameof(text));
            SpeakerId = speakerId;
            Style = style;
            Acts = acts ?? NoActs;
        }
    }

    /// <summary>
    /// ADV1本ぶんの台本（ページ列＋末尾の選択肢）。
    ///
    /// 全ページを読み終えた後に選択肢があれば選択へ、無ければそのまま終了する
    /// （＝選択肢なしの強制イベント）。純データなのでマスターデータ・JSON・
    /// コード直書きのどれからでも組める。
    /// </summary>
    public sealed class AdvScript
    {
        /// <summary>選択肢なしを表す共有インスタンス（強制イベント用）。</summary>
        private static readonly AdvChoice[] NoChoices = new AdvChoice[0];

        /// <summary>ページ列。</summary>
        public IReadOnlyList<AdvPage> Pages { get; }

        /// <summary>末尾の選択肢（空=強制イベント）。</summary>
        public IReadOnlyList<AdvChoice> Choices { get; }

        /// <summary>
        /// AdvScript を生成する。ページ0件はデータ不備として即例外
        /// （静かに空進行するより、仕込んだ時点で気づける方が安い）。
        /// </summary>
        public AdvScript(IReadOnlyList<AdvPage> pages, IReadOnlyList<AdvChoice> choices = null)
        {
            if (pages == null || pages.Count == 0)
            {
                throw new ArgumentException("ADV台本には最低1ページ必要です", nameof(pages));
            }
            for (var i = 0; i < pages.Count; i++)
            {
                if (pages[i] == null)
                {
                    throw new ArgumentException($"ページ {i} が null です", nameof(pages));
                }
            }
            Pages = pages;
            Choices = choices ?? NoChoices;
        }

        /// <summary>
        /// 本文だけの台本を作る便宜コンストラクタ（全ページ窓表示・話者なし・演技なし。
        /// ナレーションだけの強制イベントや最小の使い方向け）。
        /// </summary>
        public AdvScript(IReadOnlyList<string> texts, IReadOnlyList<AdvChoice> choices = null)
            : this(Wrap(texts), choices)
        {
        }

        /// <summary>文字列列をページ列へ包む。</summary>
        private static IReadOnlyList<AdvPage> Wrap(IReadOnlyList<string> texts)
        {
            if (texts == null || texts.Count == 0)
            {
                throw new ArgumentException("ADV台本には最低1ページ必要です", nameof(texts));
            }
            var pages = new AdvPage[texts.Count];
            for (var i = 0; i < texts.Count; i++)
            {
                pages[i] = new AdvPage(texts[i]);
            }
            return pages;
        }
    }
}
