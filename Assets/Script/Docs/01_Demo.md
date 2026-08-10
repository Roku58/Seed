# 01. 統合デモの起動と操作 — まず動かして全体像を掴む

[← 前: 00_Roadmap](00_Roadmap.md) | [索引](00_Roadmap.md) | [次: 02_Hub →](02_Hub.md)

## この章で分かること

- 統合デモ（ホーム⇄戦闘⇄ショップ）をエディタで起動する手順
- フェーズごとのキー操作と、画面・Hierarchy で確認できる見どころ
- 「永続ルート」と「フェーズ」という、本プロジェクトの2大構造の初対面
- 動かないときの典型原因（Input System 設定・Runner のアタッチ忘れ）
- 自作ゲームの入口（ルートクラス）をどう写経すればよいか

## 前提

先に読むべき章はありません。**本ガイドの最初の章であり、ここから始めます。**
Unity エディタの基本操作（シーンを開く・Play ボタン・Hierarchy / Inspector の見方）だけ分かれば読み進められます。

この章が初出の主な用語: 永続ルート / 合成ルート / フェーズ（GamePhase）/ InputRouter / エッジ検出 / 決定性 / ヒットストップ / メタAI。いずれも本文の注釈で説明します。

## 1. これは何か

`Sample_GameFlowRunner` は、本プロジェクトのほぼ全基盤（Hub・Clock・Flow・Input・UI・Data・Character・Motion・AI・StageGen・GameCore）を1本に結合した**動くデモ**です。空の GameObject にアタッチして Play するだけで、ホーム→戦闘→ショップが繋がったミニゲームが起動します。

なぜこれが最初にあるのか。各基盤は単体でも学べますが、「基盤同士がどう手を繋ぐか」は結合した実物を見るのが最短だからです。これが無いと、章ごとの知識が「部品の説明書の山」で終わり、自作ゲームの組み立て順（何を永続させ、何をフェーズ内に閉じ込めるか）が判断できません。**最初にこれを動かし、以降の章では「デモのあの動きはこの基盤だったのか」と逆引きしてください。**

> 📖 **用語 — MonoBehaviour**: Unity のコンポーネント基底クラス。GameObject にアタッチすると `Start` や `Update` などをエンジンが呼んでくれる。本プロジェクトでは MonoBehaviour を「Unity との境界」に限定し、中身は純C#クラスに寄せる方針。

> 📖 **用語 — 永続ルート**: ゲーム起動から終了まで生き続ける唯一の MonoBehaviour（本デモでは `Sample_GameFlowRunner`）。フェーズをまたいで使う基盤（Hub・Clock・Input・Flow・マスターデータ・カメラ）を所有し、毎フレームの駆動順を1箇所で決める。

> 📖 **用語 — 合成ルート**: 必要な部品を new して配線する場所を1箇所に集める設計。本プロジェクトでは「1フェーズ=1合成ルート」——各フェーズが OnEnter で自分の道具を組み、OnExit で逆順に全部片付ける。

## 2. 全体像

### 部品表（`Assets/Script/App/Samples/`）

| 部品 | 種別 | 役割 |
|---|---|---|
| `Sample_GameFlowRunner` | LifetimeScope（VContainer） | 永続ルート。「何を作り誰に渡すか」の宣言。シーンに置く唯一の部品 |
| `Sample_GameLoop` | 純C#（エントリポイント） | 「どの順で初期化し毎フレーム何を回すか」。VContainer が PlayerLoop へ橋渡し |
| `Sample_KeyboardReader` | 純C#（IInputReader） | キーボードの状態を InputSnapshot へ写すだけの最小リーダー |
| `Sample_HomePhase` / `Sample_BattlePhase` / `Sample_ShopPhase` | 純C#（GamePhase 派生） | 各画面の合成ルート。OnEnter で組み OnExit で消す |
| `Sample_PhaseIds` / `Sample_ActionIds` | 純C#（static） | フェーズID・独自アクションIDの発番台帳 |
| `Sample_MasterCatalog` / `Sample_StageSpec` | 純C# | マスターデータ（ユニット仕様＋ステージ仕様） |
| `Sample_TextPanel` | MonoBehaviour（OnGUI） | 左上のテキストメニュー。フェーズが実行時に AddComponent する |

> 📖 **用語 — OnGUI**: Unity の即時モードGUI。毎フレーム `GUI.Label` 等を呼んで描く古い仕組みで、プロダクション向きではないがデモの文字表示には最短。本デモの「== ホーム ==」等はすべてこれ。uGUI / UI Toolkit への置換候補（→ [06_UI.md](06_UI.md)）。

### データの流れ（1フレーム）

```
キーボード
   │ Sample_KeyboardReader.Read()（状態を写すだけ）
   ▼
InputRouter（エッジ検出: 押した瞬間/離した瞬間を判定）
   ▲ _input.Tick() は永続ルートが毎フレーム1回だけ呼ぶ
   │
Sample_GameLoop.Tick()（VContainer の ITickable。→ 18_Libraries.md）
   ├─ _clock.Tick(Time.deltaTime)   … dt の供給源はここだけ
   ├─ _input.Tick()
   └─ _flow.Tick(_clock.UnscaledDelta)
         │
         ▼
   現在フェーズの Tick が WasPressedThisFrame 等を読み、
   遷移したくなったら ChangePhaseCommand を Hub へ発行
         │
         ▼
   GameFlow が現フェーズの OnExit → 次フェーズの OnEnter(payload) を回す
```

> 📖 **用語 — エッジ検出**: 「押されている（状態）」と「今フレーム押された（変化の瞬間=エッジ）」の区別。InputRouter が前フレームとの差分から `WasPressedThisFrame` / `WasReleasedThisFrame` を計算する（→ [05_Input.md](05_Input.md)）。

## 3. 動かして試す

1. `Assets/Scenes/SampleScene.unity` をダブルクリックで開く（Main Camera・Directional Light・Global Volume が配置済み）。新規の空シーンでも構いません——カメラが無ければ Runner が「Main Camera」を、ライトが無ければ「Directional Light」を自動生成します
2. Hierarchy の空欄で右クリック → **Create Empty**（名前は任意。例: GameRoot）
3. その GameObject を選択したまま Inspector 最下部の **Add Component** → 検索欄に `Sample_GameFlowRunner` と入力 → クリックで追加（Inspector に設定項目はありません）
4. **Play** を押す

期待される結果:

- カメラが (0, 8, -8) から (0, 1, 0) を注視する位置へ移動する
- 画面左上に「== ホーム ==」と5行のメニュー（「[1] 出撃: 草原（敵の攻撃: ゆっくり）」「[3] ショップ」など。表示名はマスターデータ `Sample_StageSpec.DisplayName` から流し込まれる）
- Hierarchy に「HomePhase」ルートが現れる。Console に本デモ由来のログは出ません（赤いエラーが出たら本章 6節へ）

続けて触ってみる:

- **[1] で草原へ出撃**: 緑の Plane 地面、プレイヤー=UnityChan（初回はメニュー `Seed/Setup/Build UnityChan Player Prefab` を1回実行。未セットアップ環境ではカプセルへ自動フォールバック）、灰色キューブ=敵。走ると髪・スカートが揺れる（SpringBoneRig）。左上に HUD（Hunter / Monster の HP 行）。Hierarchy では「HomePhase」が消え「Stage_Grassland」が現れる
- **WASD で移動、[1] で攻撃**: 被弾の瞬間に 0.06 秒だけ世界全体が静止する（ヒットストップ）
- **決着**: リザルトに「討伐成功！   [B] ホームへ」または「力尽きた…」→ [B] でホームへ
- **[4] で迷宮へ**: 21×21 の自動生成迷路。入口側は緑床（草原帯）、最奥は赤床（溶岩帯）。最遠点に緑マーカー=出口、部屋に紫マーカー=宝箱。カメラはステージ寸法に合わせた俯瞰へ切り替わる
- **[5] で市街へ**: 31×31 の BSP 区画。茶系=住宅街、青系=市場。市場の中心に黄マーカー=店

> 📖 **用語 — Input System**: Unity の新入力パッケージ（旧 Input Manager の後継）。`Sample_KeyboardReader` は `Keyboard.current` を直読みするため、Project Settings > Player > **Active Input Handling** が「Input System Package」か「Both」であることが必須（本プロジェクトは Both 設定済み）。

## 4. コードで使う

### 最小例: フェーズ遷移は命令1発

行き先の実体を知らなくても、Hub へ命令を発行するだけでフェーズが切り替わります。ステージ指定は第2引数（payload）に `StageId.Value` を積みます。

```csharp
// 迷宮ステージ（Stage3）へ出撃する——発行者は行き先の実体を知らない
_hub.PublishCommand(new ChangePhaseCommand(
    Sample_PhaseIds.Battle,               // 行き先フェーズのID
    Sample_MasterCatalog.Stage3.Value));  // 荷物（payload）= StageId の生値
```

### 実戦例: 最小構成の永続ルート

本物の `Sample_GameFlowRunner` は **VContainer の LifetimeScope 版**です（宣言＝Runner・駆動＝`Sample_GameLoop` の分業。→ [18_Libraries.md](18_Libraries.md)）。DI を使わずに最小で組むなら、以下の MonoBehaviour 骨格でも同じ基盤 API で動きます。

```csharp
using Seed.App; using Seed.Clock; using Seed.Flow; using Seed.Hub; using Seed.Input;
using UnityEngine;

/// <summary>最小構成の永続ルート（Sample_GameFlowRunner の骨格）。</summary>
public sealed class MyGameRoot : MonoBehaviour
{
    private MessageHub _hub;            // 仲介基盤（→ 02_Hub.md）
    private ServiceRegistry _services;  // サービス台帳
    private InputRouter _input;         // 入力基盤（→ 05_Input.md）
    private GameFlow _flow;             // フロー基盤（→ 04_Flow.md）
    private GameClock _clock;           // 時間基盤（→ 03_Clock.md）

    private void Start()
    {
        // 1. フェーズをまたいで生きる基盤を生成
        _hub = new MessageHub();
        _services = new ServiceRegistry();
        _input = new InputRouter(new Sample_KeyboardReader());
        _clock = new GameClock();
        _clock.Initialize(_hub, _services);

        // 2. マスターデータ（→ 07_Data.md）
        var catalog = Sample_MasterCatalog.Build(
            new Seed.Hub.Contracts.CharacterId(1), new Seed.Hub.Contracts.CharacterId(2));

        // 3. フローへフェーズを登録し、初期フェーズを予約
        _flow = new GameFlow(_hub);
        _flow.AddPhase(new Sample_HomePhase(_hub, _input, catalog));
        _flow.AddPhase(new Sample_BattlePhase(_hub, _services, _input, catalog));
        _flow.AddPhase(new Sample_ShopPhase(_hub, _input));
        _flow.Start(Sample_PhaseIds.Home);
    }

    private void Update()
    {
        _clock.Tick(Time.deltaTime);      // dt の供給源はここだけ
        _input.Tick();                    // InputRouter の Tick は毎フレームここで1回だけ
        _flow.Tick(_clock.UnscaledDelta); // フェーズ遷移とメニューはポーズ中も動く
    }

    private void LateUpdate()
    {
        _hub.Pump(); // PublishDeferred の遅延メッセージをフレーム末尾で配達
    }

    private void OnDestroy()
    {
        _flow?.Dispose();  // 滞在中フェーズの片付けまで含めて終了
        _clock?.Dispose();
    }
}
```

本物はこの初期化・駆動・後始末を `Sample_GameFlowRunner`（宣言）と `Sample_GameLoop`（駆動）に分けて VContainer に載せたもので、役割は同じです。

> ⚠️ `Sample_HomePhase` を使うなら `Sample_BattlePhase` の登録は必須です。ホームの [1][2][4][5] は
> `ChangePhaseCommand(Battle, …)` を発行し、`GameFlow.RequestChange` は未登録フェーズに対して
> HubException（「未登録のフェーズ（AddPhase 漏れかIDの打ち間違い）」）を投げるためです。

> 📖 **用語 — LateUpdate**: すべての Update が終わった後に呼ばれる Unity のイベント関数。本デモでは `MessageHub.Pump()` をここに置き、「遅延メッセージは今フレームの連鎖の外へ回す」という約束を守っている（→ [02_Hub.md](02_Hub.md)）。

## 5. 仕組み

### キー操作一覧（フェーズ別）

物理キー→ActionId の割当は `Sample_KeyboardReader.Read` で固定です。**同じキーでも意味はフェーズごとに変わります**（意味づけは各フェーズの仕事。リーダーはキー状態を写すだけ）。

| キー | ActionId | ホーム | 戦闘 | ショップ |
|---|---|---|---|---|
| WASD | 移動ベクトル | - | 移動（カメラ相対） | - |
| マウス | 視点 | - | カメラを回す（TPS標準。戦闘中はカーソルロック） | - |
| [Space] | Jump | - | ジャンプ | - |
| [Shift] | Walk（=36） | - | 押している間は歩き（離すと走り） | - |
| [1] | Attack | 出撃: 草原（201・敵の攻撃間隔4秒） | 攻撃 | - |
| [2] | Interact | 出撃: 火山（202・敵の攻撃間隔2.5秒） | 拾う（近くのオーブへ左手を伸ばす） | - |
| [3] | Submit | ショップへ | - | - |
| [4] | Slot4（=32） | 出撃: 迷宮（203・自動生成 21×21） | - | - |
| [5] | Slot5（=33） | 出撃: 市街（204・自動生成 31×31） | - | - |
| [6] | Slot6（=37） | 出撃: 揺れものデモ（UnityChan・Playables 直駆動） | - | - |
| [7] | Slot7（=38） | イベント: よろず屋（ADV・3D。19章） | - | - |
| [8] | Slot8（=39） | イベント: 幕間の会話（ADV・2D。19章） | - | - |
| [G] | Guard | - | ガード（押している間） | - |
| [T] | Next | - | 3D⇔2D Actor 切替 | - |
| [C] | CycleView（=34） | - | 視点切替（TPS→FPS→俯瞰の巡回） | - |
| [O] | Autopilot（=35） | - | 自動操縦の切替（AI 操作） | - |
| [P] | Previous | - | ポーズ切替（ポーズ中も受付） | - |
| [B] | Cancel | - | ホームへ戻る（決着後・ポーズ中も有効） | ホームへ戻る |

生成ステージ（迷宮・市街）ではキーではなく**マーカーを踏む**ことで発火します（半径 0.8 の純C#距離判定・コライダー不要・y 無視）:

- 緑マーカー = 出口 → ホームへ帰還
- 黄マーカー = 店 → ショップフェーズへ
- 紫マーカー = 宝箱 → 鬼人薬（攻撃+15 を 20 秒。宝箱は1回で消滅）

### 内部で起きていること

Play すると `Start` で永続ルートが組まれ、以降は毎フレーム `Update` で「Clock → Input → Flow」の順に Tick が流れます。ホームの `Tick` は `WasPressedThisFrame(ActionId.Attack)` 等を監視し、押されたら `ChangePhaseCommand` を発行するだけ。処理するのは GameFlow ただ1人で、現フェーズの `OnExit`（表示物の Destroy）→ 次フェーズの `OnEnter(payload)`（舞台の再構築）を回します。戦闘フェーズは payload の StageId で仕様を引くため、**同じ戦闘フェーズに別ステージのまま再入**できます（エリア移動も同じ経路）。

三大規約との関係:

- **「状態は Tick、艶は Update」** — ゲームの真実（HP・位置・行動）は各フェーズ・各基盤の Tick で進みます。一方、ポニーテールの揺れ・注視・腕IKは Seed.Motion が LateUpdate 系で上塗りする「艶」で、真実には触れません（→ [09_Motion.md](09_Motion.md)）
- **「方針は App」** — 「被弾したらヒットストップ 0.06 秒」「HP が減るほど敵の攻撃間隔を延ばす（メタAIの手心）」といったゲームの味付けは、基盤ではなく App 層（`Sample_BattlePhase` / `Sample_ReactionPolicy` / `Sample_BattleDirector`）が持ちます
- **「命令の処理者は1基盤」** — `ChangePhaseCommand` は GameFlow だけ、`SetPausedCommand` と `HitStopCommand` は GameClock だけが処理します。発行者は誰が処理するかを知りません

> 📖 **用語 — ヒットストップ**: 打撃の瞬間に時間を短く止める演出。本デモでは `CharacterDamaged` 購読 → `HitStopCommand(0.06f)` 発行で、時間基盤への命令1発が世界全体（移動・AI・アニメ）へ同時に効く（→ [03_Clock.md](03_Clock.md)）。

> 📖 **用語 — 決定性**: 同じ入力（シード）から常に同じ結果が出る性質。生成ステージはシード = 700+StageId で設計図を作るため、同じステージへ再入すると**同じ地形・同じ配置**になる（→ [11_StageGen.md](11_StageGen.md)、リプレイとの関係は [12_GameCore.md](12_GameCore.md)）。

> 📖 **用語 — メタAI**: 個々のユニットではなく戦闘全体を采配する AI。本デモの `Sample_BattleDirector` は敵の攻撃権（間隔）を握り、プレイヤーの HP が減るほど間隔を延ばす「手心」を加える（→ [10_AI.md](10_AI.md)）。

### 見どころ（どの章の実演か）

- **イベントADV（ホーム[7]=3D / [8]=2D）**: 会話イベント（19章の `Seed.Adv`）。文字送り・吹き出し（0〜複数枚）⇄固定窓・選択肢（タップ/コントローラ）・演技（入店の移動・素振り・カメラの寄り引き・立ち絵のスライドイン）・入力ロック・イベント連鎖まで全てマスターデータ駆動。UIのガワはプレハブで差し替え可
- **標準プレイヤー（[1]〜[5] 出撃・StarterAssets）**: AnimatorController 駆動（09章の `AnimatorAvatar`）・揺れもの無し。歩き⇄走りが `Speed` のブレンドツリーで連続的に混ざる。戦闘モーション（攻撃・ガード・被弾・死亡・勝利）は Grruzam Powerful Sword Animation（大剣）から
- **揺れものデモ（[6] 出撃・UnityChan）**: Playables 直駆動＋`SpringBoneRig`（06章）。走り・急停止・ジャンプ着地で髪とスカートが揺れる。標準プレイヤーとは**完全に別のサンプル**
- **オーブを拾う（[2]）**: 草原に光るオーブが3つ浮いている（開けた場所・坂の脇・階段の上）。近づくと脈打つので [2]——左手が滑らかに伸びて（09章の `TwoBoneIkRig`）掴み、手の中で縮んで消える。伸ばしている途中に走り出すと腕は跳ねずに戻る

- 被弾の瞬間の 0.06 秒ヒットストップ → [03_Clock.md](03_Clock.md)
- プレイヤーの HP が減るほど敵の攻撃間隔が延びる → [10_AI.md](10_AI.md)
- 青カプセルの頭が常に敵を注視、3m 以内で左腕が敵へ伸びる、ポニーテールが移動・旋回で揺れる → [09_Motion.md](09_Motion.md)
- [T] で 2D 立ち絵へ切替（位置・向きは引き継ぎ、行動は Idle から再出発）→ [08_Character.md](08_Character.md)
- 生成ステージの決定性（再入で同じ地形・同じ配置）→ [11_StageGen.md](11_StageGen.md)
- Hierarchy でフェーズ入退場のたび「HomePhase」「Stage_Grassland」等のルートが生成/破棄される → [04_Flow.md](04_Flow.md)

## 6. よくあるつまずき

- **症状: キーを押しても何も反応しない** → 原因: `Sample_KeyboardReader` は Input System の `Keyboard.current` 直読みで、旧 Input Manager のみの設定では `Keyboard.current` が null になり `InputSnapshot.Empty` を返し続ける → 対処: Project Settings > Player > Active Input Handling を「Input System Package」か「Both」にする（本プロジェクトは Both 設定済みのはず。変更後はエディタ再起動を求められます）
- **症状: Play しても画面に何も出ない** → 原因: Runner のアタッチ忘れ。`SampleScene.unity` に `Sample_GameFlowRunner` は**未配置**（シーンには置かれていません） → 対処: 本章 3節の手順どおり空 GameObject へ手動でアタッチ
- **症状: 自動操縦 [O] 中に操作が効かない** → 原因: 自動操縦中は `HandlePlayerInput` が AI の入力注入後に early return し、物理キーの WASD 移動・[1]攻撃・[G]ガードを読まない（仕様） → 対処: もう一度 [O] で解除。[B]（帰還）と [P]（ポーズ）はフェーズの Tick 側の処理なので自動操縦中も有効です。[T]（Actor 切替）も early return より前で処理されるため有効です（見た目の切替は操縦者と無関係のため）
- **症状: カメラを手で置いたのに Play で位置が変わる** → 原因: Runner が Play 開始時に (0, 8, -8)→(0, 1, 0) 注視で上書きし、生成ステージ（迷宮・市街）入場時は戦闘フェーズがステージ寸法に合わせて再配置する → 対処: 仕様として受け入れる（固定ステージ入場では動かさないため、生成ステージから戻った後は俯瞰位置のまま残ります）
- **症状: [2] 火山へ出撃したはずが草原になる** → 原因: `ChangePhaseCommand` の payload が 0 または catalog に無い StageId のとき、`ResolveStage` が Stage1（草原）へフォールバックする → 対処: `Sample_MasterCatalog` に StageId が登録済みか、payload に `StageId.Value` を渡しているかを確認
- **症状: 生成ステージで敵が1体しか出ない** → 原因: GameCore サンプル世界（`Sample_ActionWorld`）が Hunter/Monster の 1v1 固定という既知の制約。実体化される敵は最初の EnemySpawn の1体のみ → 対処: 配置基盤側は複数・テーブル対応済みなので、ロジックを N 体対応にすれば実体化を回すだけ（→ [12_GameCore.md](12_GameCore.md)）

## 7. 増やす・拡張する

- **ステージを増やす**: `Sample_MasterCatalog.Build` に `Sample_StageSpec` を1件 Add し、StageId を発番（201〜帯。型が違っても ID は全体で一意の規約。Build 末尾の ValidateGlobalIdUniqueness が検証）。`Sample_HomePhase` の Tick に遷移分岐、`Lines` に表示行を追加（→ [07_Data.md](07_Data.md)）
- **フェーズを増やす**: GamePhase 派生クラスを書き、`Sample_PhaseIds` に PhaseId を1件発番し、`Sample_GameFlowRunner.Start` に `_flow.AddPhase(...)` を1行追加（→ [04_Flow.md](04_Flow.md)）
- **独自ボタンを増やす**: `Sample_ActionIds` に ActionId を発番（独自帯は 32〜63。標準予約は 1〜10）し、`Sample_KeyboardReader.Read` で物理キーへ割当（→ [05_Input.md](05_Input.md)）
- **入力を本実装化する**: `Sample_KeyboardReader` を InputSystemReader（.inputactions 駆動・リバインド可能）へ差し替え（→ [05_Input.md](05_Input.md)）
- **画面を本実装化する**: `Sample_TextPanel`（OnGUI）を uGUI / UI Toolkit の UIScreen へ置換（→ [06_UI.md](06_UI.md)）
- **シーンアセットを使うステージ**: GamePhase の CreateLoadOperation で `ISceneLoader.LoadScene` を返すと、ロード完了後に OnEnter が呼ばれる（→ [04_Flow.md](04_Flow.md)）
- **生成ステージの美術差し替え**: StageAssetPalette の Bind 行を、色付きプリミティブからプレハブ Instantiate の工場へ差し替えるだけ。生成側のコードは一切変わらない（→ [11_StageGen.md](11_StageGen.md)）

## 8. 関連ファイルとテスト

- `Assets/Script/App/Samples/Sample_GameFlowRunner.cs` — 永続ルートの実例。**自作ゲームのルートはこれの骨格を写す**
- `Assets/Script/App/Samples/Sample_KeyboardReader.cs` — 最小の入力リーダー（デモ用）
- `Assets/Script/App/Samples/Sample_HomePhase.cs` / `Sample_BattlePhase.cs` / `Sample_ShopPhase.cs` — 3フェーズの実装
- `Assets/Script/App/Samples/Sample_PhaseIds.cs` / `Sample_ActionIds.cs` — ID 発番台帳
- `Assets/Script/App/Samples/Sample_MasterCatalog.cs` / `Sample_StageSpec.cs` — マスターデータ
- `Assets/Script/App/Samples/Sample_TextPanel.cs` — OnGUI のテキストメニュー
- `Assets/Scenes/SampleScene.unity` — 起動用シーン（Runner は未配置）
- テスト: `Assets/Script/App/Tests/Editor/`（FoundationTests / HubIntegrationTests / EnemyTimerLogicTests）。実行方法は [17_Tests.md](17_Tests.md)

[← 前: 00_Roadmap](00_Roadmap.md) | [索引](00_Roadmap.md) | [次: 02_Hub →](02_Hub.md)
