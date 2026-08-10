using System;
using System.Collections.Generic;
using Seed.Adv;
using Seed.Data;

namespace Seed.App
{
    /// <summary>
    /// 【サンプル】イベントADV1本ぶんのマスターデータ（定義ID = イベントID）。
    ///
    /// 「イベントを増やす＝この定義を1件足す」が拡張の入口。ページ本文・話者・
    /// 見せ方（窓/吹き出し）・演技（移動/モーション/モデル読み替え/登場退場）・
    /// 選択肢・分岐先・登場キャラのプレハブまで全てデータで、フェーズ
    /// （Sample_AdvEventPhase）は台帳を読んで組むだけ——バリエーションは
    /// コードではなくデータとプレハブで増やす。
    ///
    /// [直列化の都合] 入れ子リスト（ページ内の演技列など）は並行配列で持ち、
    /// ページ側は PageIndex で束ねる（<see cref="BuildScript"/> が台本へ組み上げる）。
    /// </summary>
    public sealed class Sample_EventSpec : IEntityDefinition
    {
        /// <summary>定義の安定ID（イベントIDそのもの。301〜の帯）。</summary>
        public int Id { get; }

        /// <summary>ログ・エディタ表示用の識別名。</summary>
        public string DebugName { get; }

        /// <summary>ナレーション時に窓の名札へ出す見出し（店名・イベント名）。</summary>
        public string Title { get; }

        // ---- ページ（並行配列。要素数は全て同じ） ----

        /// <summary>ページ本文（1要素=1ページ。文字送りで表示される）。</summary>
        public string[] PageTexts { get; }

        /// <summary>ページごとの話者キャラID（0=ナレーション）。</summary>
        public int[] PageSpeakers { get; }

        /// <summary>ページごとの見せ方（0=窓 / 1=吹き出し。AdvSpeechStyle の値）。</summary>
        public int[] PageStyles { get; }

        // ---- 演技（並行配列。ActPages の値=どのページの開始時に実行するか） ----

        /// <summary>演技の対象ページ番号。</summary>
        public int[] ActPages { get; }

        /// <summary>演技の対象キャラID。</summary>
        public int[] ActActors { get; }

        /// <summary>演技の種類（AdvActKind の値）。</summary>
        public int[] ActKinds { get; }

        /// <summary>演技の文字列パラメータ（Motion=ステート名 / Model=プレハブキー）。</summary>
        public string[] ActKeys { get; }

        /// <summary>演技の補助文字列（Model=コントローラキー）。</summary>
        public string[] ActSubKeys { get; }

        /// <summary>演技の位置 X（Move）。</summary>
        public float[] ActX { get; }

        /// <summary>演技の位置 Y（Move）。</summary>
        public float[] ActY { get; }

        /// <summary>演技の位置 Z（Move）。</summary>
        public float[] ActZ { get; }

        /// <summary>演技の所要秒数（Move/Camera=補間時間 / Bubble/Timeline=時間寿命）。</summary>
        public float[] ActSeconds { get; }

        /// <summary>演技の寿命（AdvActLife の値。0=閉じ命令まで / 1=ページ切替で / 2=送り操作で）。</summary>
        public int[] ActLives { get; }

        // ---- 登場キャラ（並行配列） ----

        /// <summary>登場キャラのID（ページの話者・演技の対象として参照される）。</summary>
        public int[] ActorIds { get; }

        /// <summary>登場キャラの表示名（窓の名札に出す）。</summary>
        public string[] ActorNames { get; }

        /// <summary>登場キャラのプレハブ（Resources パス。空=カプセルで代替）。</summary>
        public string[] ActorPrefabs { get; }

        /// <summary>登場キャラへ割り当てる AnimatorController（Resources パス。空=そのまま）。</summary>
        public string[] ActorControllers { get; }

        /// <summary>初期位置 X。</summary>
        public float[] ActorX { get; }

        /// <summary>初期位置 Y。</summary>
        public float[] ActorY { get; }

        /// <summary>初期位置 Z。</summary>
        public float[] ActorZ { get; }

        /// <summary>吹き出しの表示位置オフセット X（キャラ位置に加算＝既定の吹き出し位置）。</summary>
        public float[] BubbleX { get; }

        /// <summary>吹き出しの表示位置オフセット Y。</summary>
        public float[] BubbleY { get; }

        /// <summary>吹き出しの表示位置オフセット Z。</summary>
        public float[] BubbleZ { get; }

        // ---- 選択肢・遷移・装飾 ----

        /// <summary>選択肢のラベル（空=選択肢なしの強制イベント）。</summary>
        public string[] ChoiceLabels { get; }

        /// <summary>選択肢ごとの結果（次のイベントID。0=イベントを閉じる）。</summary>
        public int[] ChoiceResults { get; }

        /// <summary>強制イベント読了後の遷移先イベントID（0=閉じる。選択肢ありでは未使用）。</summary>
        public int NextEventId { get; }

        /// <summary>背景のプレハブ（Resources パス。空=内蔵の色板で代替）。</summary>
        public string BackgroundPrefabKey { get; }

        /// <summary>文字送り速度（文字/秒。0以下=瞬間表示）。</summary>
        public float CharsPerSecond { get; }

        /// <summary>舞台モード（0=3D: ワールドにモデル / 1=2D: キャンバスに立ち絵）。</summary>
        public int StageMode { get; }

        /// <summary>UIガワのプレハブ（Sample_AdvUiView 付き。空=内蔵の既定ガワ）。</summary>
        public string UiPrefabKey { get; }

        /// <summary>再生中に他の入力（[B] 退出など）を停止するか（既定=停止）。</summary>
        public bool LockOtherInput { get; }

        /// <summary>切替用カメラのID（CameraSwitch の切替先。空=カメラ切替なし）。</summary>
        public int[] CamIds { get; }

        /// <summary>切替用カメラの位置 X。</summary>
        public float[] CamX { get; }

        /// <summary>切替用カメラの位置 Y。</summary>
        public float[] CamY { get; }

        /// <summary>切替用カメラの位置 Z。</summary>
        public float[] CamZ { get; }

        /// <summary>切替用カメラの注視先キャラID（0=正面）。</summary>
        public int[] CamLookActors { get; }

        /// <summary>Sample_EventSpec を生成する（並行配列の数が揃わないデータ不備は即例外）。</summary>
        public Sample_EventSpec(int id, string debugName, string title,
            string[] pageTexts, int[] pageSpeakers = null, int[] pageStyles = null,
            int[] actPages = null, int[] actActors = null, int[] actKinds = null,
            string[] actKeys = null, string[] actSubKeys = null,
            float[] actX = null, float[] actY = null, float[] actZ = null,
            float[] actSeconds = null, int[] actLives = null,
            int[] actorIds = null, string[] actorNames = null, string[] actorPrefabs = null,
            string[] actorControllers = null,
            float[] actorX = null, float[] actorY = null, float[] actorZ = null,
            float[] bubbleX = null, float[] bubbleY = null, float[] bubbleZ = null,
            string[] choiceLabels = null, int[] choiceResults = null,
            int nextEventId = 0, string backgroundPrefabKey = "", float charsPerSecond = 30f,
            int stageMode = 0, string uiPrefabKey = "", bool lockOtherInput = true,
            int[] camIds = null, float[] camX = null, float[] camY = null,
            float[] camZ = null, int[] camLookActors = null)
        {
            Id = id;
            DebugName = debugName;
            Title = title ?? "";
            PageTexts = pageTexts ?? Array.Empty<string>();
            PageSpeakers = FillOrValidate(pageSpeakers, PageTexts.Length, id, "PageSpeakers");
            PageStyles = FillOrValidate(pageStyles, PageTexts.Length, id, "PageStyles");

            ActPages = actPages ?? Array.Empty<int>();
            ActActors = Require(actActors, ActPages.Length, id, "ActActors");
            ActKinds = Require(actKinds, ActPages.Length, id, "ActKinds");
            ActKeys = RequireOrEmpty(actKeys, ActPages.Length, id, "ActKeys");
            ActSubKeys = RequireOrEmpty(actSubKeys, ActPages.Length, id, "ActSubKeys");
            ActX = RequireOrZero(actX, ActPages.Length, id, "ActX");
            ActY = RequireOrZero(actY, ActPages.Length, id, "ActY");
            ActZ = RequireOrZero(actZ, ActPages.Length, id, "ActZ");
            ActSeconds = RequireOrZero(actSeconds, ActPages.Length, id, "ActSeconds");
            ActLives = FillOrValidate(actLives, ActPages.Length, id, "ActLives");

            ActorIds = actorIds ?? Array.Empty<int>();
            ActorNames = RequireOrEmpty(actorNames, ActorIds.Length, id, "ActorNames");
            ActorPrefabs = RequireOrEmpty(actorPrefabs, ActorIds.Length, id, "ActorPrefabs");
            ActorControllers = RequireOrEmpty(actorControllers, ActorIds.Length, id, "ActorControllers");
            ActorX = RequireOrZero(actorX, ActorIds.Length, id, "ActorX");
            ActorY = RequireOrZero(actorY, ActorIds.Length, id, "ActorY");
            ActorZ = RequireOrZero(actorZ, ActorIds.Length, id, "ActorZ");
            BubbleX = RequireOrZero(bubbleX, ActorIds.Length, id, "BubbleX");
            BubbleY = RequireOrZero(bubbleY, ActorIds.Length, id, "BubbleY");
            BubbleZ = RequireOrZero(bubbleZ, ActorIds.Length, id, "BubbleZ");

            ChoiceLabels = choiceLabels ?? Array.Empty<string>();
            ChoiceResults = choiceResults ?? Array.Empty<int>();
            if (ChoiceLabels.Length != ChoiceResults.Length)
            {
                throw new ArgumentException(
                    $"イベント {id}: 選択肢のラベル({ChoiceLabels.Length})と結果({ChoiceResults.Length})の数が一致していません");
            }
            NextEventId = nextEventId;
            BackgroundPrefabKey = backgroundPrefabKey ?? "";
            CharsPerSecond = charsPerSecond;
            StageMode = stageMode;
            UiPrefabKey = uiPrefabKey ?? "";
            LockOtherInput = lockOtherInput;
            CamIds = camIds ?? Array.Empty<int>();
            CamX = RequireOrZero(camX, CamIds.Length, id, "CamX");
            CamY = RequireOrZero(camY, CamIds.Length, id, "CamY");
            CamZ = RequireOrZero(camZ, CamIds.Length, id, "CamZ");
            CamLookActors = Require(camLookActors, CamIds.Length, id, "CamLookActors");
        }

        /// <summary>台本（Seed.Adv の AdvScript）へ組み上げる。</summary>
        public AdvScript BuildScript()
        {
            var pages = new AdvPage[PageTexts.Length];
            for (var i = 0; i < PageTexts.Length; i++)
            {
                List<AdvAct> acts = null;
                for (var j = 0; j < ActPages.Length; j++)
                {
                    if (ActPages[j] != i)
                    {
                        continue;
                    }
                    acts ??= new List<AdvAct>();
                    acts.Add(new AdvAct(ActActors[j], (AdvActKind)ActKinds[j],
                        ActKeys[j], ActSubKeys[j], ActX[j], ActY[j], ActZ[j], ActSeconds[j],
                        (AdvActLife)ActLives[j]));
                }
                pages[i] = new AdvPage(PageTexts[i], PageSpeakers[i],
                    (AdvSpeechStyle)PageStyles[i], acts);
            }

            AdvChoice[] choices = null;
            if (ChoiceLabels.Length > 0)
            {
                choices = new AdvChoice[ChoiceLabels.Length];
                for (var i = 0; i < choices.Length; i++)
                {
                    choices[i] = new AdvChoice(ChoiceLabels[i], ChoiceResults[i]);
                }
            }
            return new AdvScript(pages, choices);
        }

        /// <summary>null は全0で埋め、指定があれば数の一致を検証する。</summary>
        private static int[] FillOrValidate(int[] values, int count, int id, string name)
        {
            if (values == null)
            {
                return count == 0 ? Array.Empty<int>() : new int[count];
            }
            if (values.Length != count)
            {
                throw new ArgumentException($"イベント {id}: {name} の数({values.Length})がページ数({count})と一致していません");
            }
            return values;
        }

        /// <summary>指定必須の並行配列（数の一致を検証）。</summary>
        private static int[] Require(int[] values, int count, int id, string name)
        {
            values ??= Array.Empty<int>();
            if (values.Length != count)
            {
                throw new ArgumentException($"イベント {id}: {name} の数({values.Length})が期待({count})と一致していません");
            }
            return values;
        }

        /// <summary>null は空文字で埋め、指定があれば数の一致を検証する。</summary>
        private static string[] RequireOrEmpty(string[] values, int count, int id, string name)
        {
            if (values == null)
            {
                var filled = new string[count];
                for (var i = 0; i < count; i++)
                {
                    filled[i] = "";
                }
                return filled;
            }
            if (values.Length != count)
            {
                throw new ArgumentException($"イベント {id}: {name} の数({values.Length})が期待({count})と一致していません");
            }
            return values;
        }

        /// <summary>null は全0で埋め、指定があれば数の一致を検証する。</summary>
        private static float[] RequireOrZero(float[] values, int count, int id, string name)
        {
            if (values == null)
            {
                return count == 0 ? Array.Empty<float>() : new float[count];
            }
            if (values.Length != count)
            {
                throw new ArgumentException($"イベント {id}: {name} の数({values.Length})が期待({count})と一致していません");
            }
            return values;
        }
    }
}
