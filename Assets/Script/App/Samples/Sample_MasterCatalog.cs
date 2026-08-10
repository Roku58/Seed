using Seed.Data;
using Seed.Hub.Contracts;
using UnityEngine;

namespace Seed.App
{
    /// <summary>
    /// 【サンプル】このアプリのマスターデータカタログ（ユニット仕様＋ステージ仕様）。
    /// 実プロジェクトでは ScriptableObject / JSON → MasterDataLoader で流し込む部分。
    /// 「キャラやステージを増やす＝ここに1件足す（将来はデータを1つ足す）」。
    /// ID帯の運用: 1〜99=ユニット / 201〜=ステージ（型が違ってもIDは全体で一意にする）。
    /// </summary>
    public static class Sample_MasterCatalog
    {
        /// <summary>ステージ1（草原）。</summary>
        public static readonly StageId Stage1 = new StageId(201);

        /// <summary>ステージ2（火山）。</summary>
        public static readonly StageId Stage2 = new StageId(202);

        /// <summary>ステージ3（自動生成: 迷宮）。</summary>
        public static readonly StageId Stage3 = new StageId(203);

        /// <summary>ステージ4（自動生成: 市街）。</summary>
        public static readonly StageId Stage4 = new StageId(204);

        /// <summary>ステージ5（揺れものデモ: UnityChan・Playables 直駆動）。</summary>
        public static readonly StageId Stage5 = new StageId(205);

        /// <summary>イベント: よろず屋（ADVデモの入口。ID帯 301〜=イベント）。</summary>
        public const int EventShop = 301;

        /// <summary>イベント: 回復薬の購入（強制テキスト→店へ戻る）。</summary>
        public const int EventBuyPotion = 302;

        /// <summary>イベント: 大剣の研ぎ（強制テキスト→店へ戻る）。</summary>
        public const int EventSharpen = 303;

        /// <summary>イベント: 幕間の会話（2D舞台のADVデモ）。</summary>
        public const int EventTalk2D = 311;

        /// <summary>イベント: 迷宮へ（2D・入力ロックの実演）。</summary>
        public const int EventDive2D = 312;

        /// <summary>カタログを組み立てる（起動時に1回）。</summary>
        public static MasterDataSet Build(CharacterId playerId, CharacterId enemyId)
        {
            var catalog = new MasterDataSet();

            // ユニット仕様（定義ID = CharacterId と同値運用）
            catalog.Add(new Sample_UnitSpec(playerId.Value, "Hunter",
                moveSpeed: 4f, attackSeconds: 0.4f, staggerSeconds: 0.25f));
            catalog.Add(new Sample_UnitSpec(enemyId.Value, "Monster",
                moveSpeed: 2f, attackSeconds: 0.5f, staggerSeconds: 0.25f));

            // ステージ仕様（定義ID = StageId と同値運用）
            catalog.Add(new Sample_StageSpec(Stage1.Value, "Grassland", "草原（敵の攻撃: ゆっくり）",
                groundColor: new Color(0.35f, 0.6f, 0.3f), groundScale: 2f, enemyAttackInterval: 4f));
            catalog.Add(new Sample_StageSpec(Stage2.Value, "Volcano", "火山（敵の攻撃: 速い）",
                groundColor: new Color(0.55f, 0.25f, 0.15f), groundScale: 1.5f, enemyAttackInterval: 2.5f));

            // 自動生成ステージ（地形・配置は Seed.StageGen がシードから決定的に作る）
            catalog.Add(new Sample_StageSpec(Stage3.Value, "Labyrinth", "迷宮（自動生成・出口を探せ）",
                groundColor: Color.gray, groundScale: 1f, enemyAttackInterval: 3.5f,
                generatorKind: 1, genWidth: 21, genHeight: 21, braidPermille: 200));
            catalog.Add(new Sample_StageSpec(Stage4.Value, "Township", "市街（自動生成・店に寄れる）",
                groundColor: Color.gray, groundScale: 1f, enemyAttackInterval: 3.0f,
                generatorKind: 2, genWidth: 31, genHeight: 31));

            // 揺れものデモ（UnityChan・Playables 直駆動。標準は StarterAssets・Controller 駆動）
            catalog.Add(new Sample_StageSpec(Stage5.Value, "SpringDemo", "揺れものデモ（UnityChan）",
                groundColor: new Color(0.35f, 0.55f, 0.4f), groundScale: 2f, enemyAttackInterval: 4f,
                playerModelKind: 1));

            // イベントADV（会話・吹き出し・演技・分岐まで全てデータ。フェーズは読むだけ）
            AddEvents(catalog);

            catalog.ValidateGlobalIdUniqueness();
            return catalog;
        }

        /// <summary>イベントADVの台帳（よろず屋の一連。増やす＝ここに1件足す）。</summary>
        private static void AddEvents(MasterDataSet catalog)
        {
            // 登場キャラは3イベント共通: 1=店主（StarterAssets） / 2=相棒（UnityChan）
            var actorIds = new[] { 1, 2 };
            var actorNames = new[] { "店主", "相棒" };
            var actorPrefabs = new[] { "StarterPlayer", "PlayerModel" };
            var actorControllers = new[] { "StarterAnimator", "PlayerAnimator" };
            var bubbleY = new[] { 2.05f, 1.7f };
            var zeros2 = new float[2];

            // 301 よろず屋: 相棒が入店（演技: 移動）→吹き出しの掛け合い→窓→選択肢
            catalog.Add(new Sample_EventSpec(EventShop, "EventShop", "よろず屋",
                pageTexts: new[]
                {
                    "いらっしゃい。冒険者かい？\nこんな迷宮の奥まで来るとは、あんたも物好きだねえ。",
                    "すごい、本当にお店があった……！\nねえねえ、掘り出し物はある？",
                    "品はどれも一点もの。\nゆっくり見ていっておくれ。",
                },
                pageSpeakers: new[] { 1, 2, 1 },
                pageStyles: new[] { 1, 1, 0 },
                actPages: new[] { 0, 1, 1, 2 },
                actActors: new[] { 2, 2, 1, 1 },
                actKinds: new[] { 0, 1, 5, 1 }, // Move / Motion / Bubble（ページ寿命） / Motion
                actKeys: new[] { "", "Win", "（ほう、連れがいたのかい……）", "Guard" },
                actSubKeys: new[] { "", "", "", "" },
                actX: new[] { -0.85f, 0f, 0f, 0f },
                actY: new float[4],
                actZ: new[] { 2.3f, 0f, 0f, 0f },
                actSeconds: new[] { 1.5f, 0f, 0f, 0f },
                actLives: new[] { 0, 0, 1, 0 }, // 独り言はページ切替で自動で閉じる
                actorIds: actorIds, actorNames: actorNames,
                actorPrefabs: actorPrefabs, actorControllers: actorControllers,
                actorX: new[] { 0.75f, -2.8f }, actorY: zeros2, actorZ: new[] { 2.6f, 3.8f },
                bubbleX: zeros2, bubbleY: bubbleY, bubbleZ: zeros2,
                choiceLabels: new[] { "回復薬を買う", "大剣を研いでもらう", "店を出る" },
                choiceResults: new[] { EventBuyPotion, EventSharpen, 0 },
                charsPerSecond: 32f,
                lockOtherInput: false)); // 店では [B] で退出できる（強制演出中はロック＝既定）

            // 302 回復薬（強制イベント: 選択肢なし→店へ戻る）
            catalog.Add(new Sample_EventSpec(EventBuyPotion, "EventBuyPotion", "よろず屋",
                pageTexts: new[]
                {
                    "まいど！\u3000傷はこまめに治すもんだよ。",
                    "回復薬を手に入れた。",
                },
                pageSpeakers: new[] { 1, 0 },
                pageStyles: new[] { 1, 0 },
                actPages: new[] { 0, 1 },
                actActors: new[] { 1, 2 },
                actKinds: new[] { 1, 1 },
                actKeys: new[] { "Win", "Win" },
                actSubKeys: new[] { "", "" },
                actX: new float[2], actY: new float[2], actZ: new float[2],
                actSeconds: new float[2],
                actorIds: actorIds, actorNames: actorNames,
                actorPrefabs: actorPrefabs, actorControllers: actorControllers,
                actorX: new[] { 0.75f, -0.85f }, actorY: zeros2, actorZ: new[] { 2.6f, 2.3f },
                bubbleX: zeros2, bubbleY: bubbleY, bubbleZ: zeros2,
                nextEventId: EventShop,
                charsPerSecond: 36f));

            // 303 大剣の研ぎ（強制イベント: 店主の素振り演技つき→店へ戻る）
            catalog.Add(new Sample_EventSpec(EventSharpen, "EventSharpen", "よろず屋",
                pageTexts: new[]
                {
                    "大剣を見せてみな。……ふむ、いい業物だ。\nちょいと借りるよ。",
                    "研ぎ澄まされた大剣が返ってきた。\n攻撃力が上がった気がする！",
                },
                pageSpeakers: new[] { 1, 0 },
                pageStyles: new[] { 1, 0 },
                actPages: new[] { 0, 0, 1, 1 },
                actActors: new[] { 2, 1, 1, 0 },
                actKinds: new[] { 12, 1, 1, 11 }, // CameraSwitch（寄り。ページ寿命で自動復帰）/ Motion / Motion / Signal
                actKeys: new[] { "", "Attack", "Win", "SharpenDone" },
                actSubKeys: new[] { "", "", "", "" },
                actX: new float[4],
                actY: new float[4],
                actZ: new float[4],
                actSeconds: new[] { 0.7f, 0f, 0f, 0f },
                actLives: new[] { 1, 0, 0, 0 },
                actorIds: actorIds, actorNames: actorNames,
                actorPrefabs: actorPrefabs, actorControllers: actorControllers,
                actorX: new[] { 0.75f, -0.85f }, actorY: zeros2, actorZ: new[] { 2.6f, 2.3f },
                bubbleX: zeros2, bubbleY: bubbleY, bubbleZ: zeros2,
                nextEventId: EventShop,
                charsPerSecond: 32f,
                camIds: new[] { 1, 2 },                       // 1=引き（既定） / 2=店主への寄り
                camX: new[] { 0f, 0.35f },
                camY: new[] { 1.35f, 1.5f },
                camZ: new[] { 0f, 1.35f },
                camLookActors: new[] { 0, 1 }));

            // 311 幕間の会話（2D舞台: 立ち絵のスライドイン・吹き出しの重ね・選択肢）
            catalog.Add(new Sample_EventSpec(EventTalk2D, "EventTalk2D", "幕間",
                pageTexts: new[]
                {
                    "ここが迷宮の入口か……。\n思ったより静かだな。",
                    "先輩、準備はいいですか？\nわたし、ちょっとワクワクしてます。",
                    "二人は顔を見合わせた。",
                },
                pageSpeakers: new[] { 1, 2, 0 },
                pageStyles: new[] { 1, 1, 0 },
                actPages: new[] { 0, 0, 1 },
                actActors: new[] { 1, 2, 1 },
                actKinds: new[] { 0, 0, 5 }, // Move（両者スライドイン）/ Bubble（送り寿命）
                actKeys: new[] { "", "", "（頼もしいことで……）" },
                actSubKeys: new[] { "", "", "" },
                actX: new[] { -430f, 430f, 0f },
                actY: new[] { -40f, -40f, 0f },
                actZ: new float[3],
                actSeconds: new[] { 0.6f, 0.6f, 0f },
                actLives: new[] { 0, 0, 2 }, // 独り言は次の送り操作（タップ）で閉じる
                actorIds: new[] { 1, 2 },
                actorNames: new[] { "先輩", "後輩" },
                actorPrefabs: new[] { "", "" }, // 立ち絵プレハブを置けば差し替わる（無ければ色板＋名前）
                actorControllers: new[] { "", "" },
                actorX: new[] { -900f, 900f }, // 画面外＝Move の演技で入場する
                actorY: new[] { -40f, -40f },
                actorZ: new float[2],
                bubbleX: new float[2],
                bubbleY: new[] { 380f, 380f }, // 立ち絵の頭上（キャンバスpx）
                bubbleZ: new float[2],
                choiceLabels: new[] { "迷宮へ進む", "引き返す" },
                choiceResults: new[] { EventDive2D, 0 },
                stageMode: 1,
                charsPerSecond: 34f,
                lockOtherInput: false));

            // 312 迷宮へ（2D・強制イベント。入力ロック＝既定のまま: [B] で抜けられない実演）
            catalog.Add(new Sample_EventSpec(EventDive2D, "EventDive2D", "幕間",
                pageTexts: new[]
                {
                    "二人は迷宮へ足を踏み入れた。\n——冒険が始まる。",
                },
                actorIds: new[] { 1, 2 },
                actorNames: new[] { "先輩", "後輩" },
                actorPrefabs: new[] { "", "" },
                actorControllers: new[] { "", "" },
                actorX: new[] { -430f, 430f },
                actorY: new[] { -40f, -40f },
                actorZ: new float[2],
                bubbleX: new float[2],
                bubbleY: new[] { 380f, 380f },
                bubbleZ: new float[2],
                stageMode: 1,
                charsPerSecond: 28f));
        }
    }
}
