# 04. Seed.Flow — フェーズ遷移とステージ切替
[← 前: 03_Clock](03_Clock.md) | [索引](00_Roadmap.md) | [次: 05_Input →](05_Input.md)

## この章で分かること

- ホーム・戦闘・ショップといった「ゲームの大きな場面」を `GameFlow` でどう切り替えるか
- 遷移のライフサイクル「要求 → OnExit → 非同期ロード待ち → OnEnter → PhaseChanged 発行」の正確な時系列
- **ステージ切替 = 同じフェーズへ別 payload で再入する**という Seed 流の考え方
- デモの入口 `Sample_GameFlowRunner`（永続ルート）の骨格コードの読み方
- シーンアセットを使うステージへの拡張口（`IFlowOperation` / `ISceneLoader`）

## 前提

- [02_Hub.md](02_Hub.md) — `MessageHub` の命令（`PublishCommand` / `SubscribeCommand`）と通知（`Publish` / `Subscribe`）、
  `HubException`、`ServiceRegistry`。この章の遷移要求はすべて命令 `ChangePhaseCommand` で流れます
- [03_Clock.md](03_Clock.md) — `GameClock` が供給する `ScaledDelta` / `UnscaledDelta`。
  `GameFlow` を **UnscaledDelta で回す**理由（ポーズ中もメニュー遷移を効かせる）がこの章で回収されます

この章が初出の主な用語: **状態機械 / フェーズ / payload（荷物）/ 永続ルート / 合成ルート / latest wins / ポーリング**

## 1. これは何か

`GameFlow` は、ゲームフェーズ（ホーム・戦闘・ショップ…）の遷移を一手に引き受ける**状態機械**です。

> 📖 **用語 — 状態機械（ステートマシン）**: 「今どの状態にいるか」を1つだけ持ち、決められた手順でしか
> 状態を切り替えない仕組み。切り替えの入口を1か所に絞ることで「片付け忘れ」「二重初期化」を構造的に防ぎます。

これが無いと何に困るか。フェーズ切替を各画面が勝手にやると、
「戦闘の後片付けを忘れたままホームを組み立てる」「ロード中に別の遷移が割り込んで世界が二重になる」
といった事故が起きます。`GameFlow` は次の3つを強制することでこれを防ぎます。

- **`ChangePhaseCommand` の唯一の処理者**である（`SubscribeCommand` が単独処理者を強制。[02_Hub.md](02_Hub.md)）。
  どの基盤・画面からでも命令1発で遷移を頼めるが、遷移の作法は `GameFlow` しか知らない
- 遷移は「要求 → 次 Tick で OnExit → 非同期ロード待ち → OnEnter → `PhaseChanged` 発行」の**一本道**しかない
- **同一フェーズへの再入も完全に Exit→Enter を回す**。だからステージ切替は
  「同じ戦闘フェーズへ別の payload（StageId）で再入」するだけで表現できる

> 📖 **用語 — payload（荷物）**: 遷移に添える `int` 1個。意味はアプリが決めます（デモでは StageId の値）。
> `ChangePhaseCommand(Battle, 202)` なら「戦闘フェーズへ、ステージ202の指定つきで」という意味になります。

> 📖 **用語 — 永続ルート / 合成ルート**: 合成ルートは「基盤を new して配線する場所」の総称。
> そのうちアプリ起動から終了まで生き残るものが永続ルート（デモでは `Sample_GameFlowRunner`）。
> Seed の規約「1フェーズ=1合成ルート」では、各フェーズの `OnEnter` も合成ルートの一種です。

## 2. 全体像

### 部品表

| 部品 | 種別 | 役割 |
|---|---|---|
| `GameFlow` | 純C#（sealed, `IDisposable`） | 遷移状態機械の本体。`ChangePhaseCommand` の唯一の処理者 |
| `GamePhase` | 純C# 抽象基底 | フェーズ1つの型。`Id` / `CreateLoadOperation` / `OnEnter` / `OnExit` / `Tick` |
| `IFlowOperation` | インターフェース | 遷移中の非同期作業の進捗契約（`IsDone` / `Progress`） |
| `CompletedFlowOperation` | 純C#（共有 `Instance`） | 即時完了の実装（同期フェーズ・テスト用） |
| `ISceneLoader` | インターフェース | シーンロード抽象（`LoadScene` / `UnloadScene`） |
| `UnitySceneLoader` | 純C# | `ISceneLoader` 本番実装。`SceneManager` の `AsyncOperation` を包む |
| `PhaseId` | `readonly struct`（Hub/Contracts） | フェーズを指す共有ID。0 は `PhaseId.None` 予約 |
| `ChangePhaseCommand` / `PhaseChanged` | `readonly struct`（Hub/Contracts） | 遷移の命令（依頼）と完了通知（過去形） |

> 📖 **用語 — IDisposable**: 「使い終わったら `Dispose()` で片付ける」ことを型で約束する C# の標準
> インターフェース。`GameFlow.Dispose()` は滞在中フェーズの `OnExit` まで面倒を見ます。

### 遷移のライフサイクル（時系列図）

```text
フレーム N    どこかの基盤: hub.PublishCommand(new ChangePhaseCommand(Battle, 202))
              └ GameFlow が購読コールバックで受信 → RequestChange → _pending に「予約」だけ
                （この時点では何も壊れない。現フェーズはこのフレームを普通に生き延びる）

フレーム N+1  flow.Tick():
              ├ 予約を検出 → 現フェーズ.OnExit()          … 合成ルートの片付け
              ├ 次フェーズ.CreateLoadOperation(202)        … null なら「即時」扱い
              └ ここで return（この Tick ではどのフェーズも駆動されない）

フレーム N+2〜 flow.Tick():                                 ※ ロードがある場合のみ
              └ _loading.IsDone を確認。false の間は毎フレームここで return
                （UI は IsTransitioning / LoadProgress を読んでローディング表示）

完了フレーム  flow.Tick():
              ├ 次フェーズ.OnEnter(202)                    … 合成ルートの組み立て
              ├ hub.Publish(new PhaseChanged(prev, cur, 202)) … 過去形の通知を発行
              └ 同じ Tick 内で新フェーズの Tick(dt) が呼ばれる（初回駆動）
```

押さえるべきポイントは3つです。

- **要求は予約であり、実行は必ず次の `Tick`**。フェーズ自身の `Tick` の最中に遷移を要求しても、
  実行中のフェーズがスタック上で破棄されることがない（遅延実行で構造的に安全）
- **退場したフレームは誰も `Tick` されない**「空白の1フレーム」がある。ロード中も同様
- `OnEnter` が終わって初めて `PhaseChanged`（Previous / Current / Payload）が発行される。
  初回起動時の Previous は `PhaseId.None`

## 3. 動かして試す

1. File > New Scene で新規シーンを作成します（カメラ・ライトは不要。Runner が無ければ自動生成します）
2. Hierarchy で右クリック → Create Empty で空の GameObject を作成します
3. その GameObject を選択し、Inspector の Add Component から **Sample_GameFlowRunner** を追加します
   （`Assets/Script/App/Samples/Sample_GameFlowRunner.cs`。Inspector の設定項目はありません）
4. Play を押します。画面左上にテキストパネル（Title「ホーム」、`[1]`〜`[5]` のメニュー行）が表示され、
   **Hierarchy に「HomePhase」という GameObject が生えます**
5. **[1]** を押すと草原へ出撃します。Hierarchy で「HomePhase」が Destroy され、
   「Stage_Grassland」等のステージルートが生成されます（OnExit→OnEnter が完全に回った証拠です）
6. **[B]** でホームへ戻り、**[2]** で再出撃します。**同じ `Sample_BattlePhase` に別の StageId（202=火山）で
   再入**し、地面の色・広さ・敵の攻撃間隔が差し替わります。これが「同一フェーズ再入=ステージ切替」です
7. 戦闘中に **[P]** でポーズします。戦闘の進行は止まりますが、**[B] でホームへ戻る操作は効きます**
   （`GameFlow` が `UnscaledDelta` 駆動のため。[03_Clock.md](03_Clock.md)）
8. **[4]** / **[5]** は自動生成ステージ（迷宮203・市街204）への出撃です。ホームのキーはすべて
   `ChangePhaseCommand(Battle, StageId.Value)` の発行にすぎません

Console には既定では何も出力されません。遷移を目で追いたい場合は、`PhaseChanged` を購読して
`Debug.Log` する数行を Runner に足すのが手軽です（本章 4節の実戦例の後に載せます）。

## 4. コードで使う

### 最小例 — フェーズ2つを回す

```csharp
using Seed.Clock;
using Seed.Flow;
using Seed.Hub;
using Seed.Hub.Contracts;

// --- フェーズは GamePhase を継承して書く（純C#。MonoBehaviour ではない） ---
public sealed class MyHomePhase : GamePhase
{
    private readonly MessageHub _hub;
    private UnityEngine.GameObject _root;

    public MyHomePhase(MessageHub hub) { _hub = hub; }

    public override PhaseId Id => new PhaseId(1);          // ID の発番はアプリの知識（1以上）

    public override void OnEnter(int payload)              // 入場: 合成ルートの組み立て
    {
        _root = new UnityEngine.GameObject("HomePhase");
    }

    public override void Tick(float deltaTime)              // 滞在中の毎フレーム駆動
    {
        // メニュー入力を検出したら命令1発（実行は次の Tick なのでここから呼んでも安全）
        // _hub.PublishCommand(new ChangePhaseCommand(new PhaseId(2), payload: 201));
    }

    public override void OnExit()                            // 退場: 組み立ての逆順で片付け
    {
        UnityEngine.Object.Destroy(_root);
        _root = null;
    }
}

// --- 永続ルート（MonoBehaviour）の Start で ---
var hub = new MessageHub();
var flow = new GameFlow(hub);            // ChangePhaseCommand の唯一の処理者になる
flow.AddPhase(new MyHomePhase(hub));     // フェーズ登録（同一IDの二重登録は HubException）
flow.AddPhase(new MyBattlePhase(hub));   // Id = new PhaseId(2) のフェーズ
flow.Start(new PhaseId(1));              // 予約のみ。実際の入場は次の Tick

// --- 毎フレーム（Update）---
clock.Tick(UnityEngine.Time.deltaTime);  // dt の加工が何より先（03_Clock）
flow.Tick(clock.UnscaledDelta);          // ポーズ中もメニュー遷移を効かせる

// --- どこからでも命令1発 ---
hub.PublishCommand(new ChangePhaseCommand(new PhaseId(2), payload: 201)); // ステージ201へ

// --- 終了時 ---
flow.Dispose();                          // 滞在フェーズの OnExit まで面倒を見る
```

### 実戦例 — `Sample_GameFlowRunner`（永続ルート）の骨格

デモの入口はこの1個の MonoBehaviour です。「フェーズをまたぐものは永続ルートが持ち、
その場でだけ使うものはフェーズが持つ」という役割分担がそのままコードになっています。

```csharp
public sealed class Sample_GameFlowRunner : MonoBehaviour
{
    private MessageHub _hub;          // 仲介基盤（02_Hub）
    private ServiceRegistry _services;
    private InputRouter _input;       // 入力基盤（05_Input）
    private GameFlow _flow;           // この章の主役
    private GameClock _clock;         // 時間基盤（03_Clock）

    private void Start()
    {
        // 1. フェーズをまたいで生きる基盤（永続ルートの所有物）
        _hub = new MessageHub();
        _services = new ServiceRegistry();
        _input = new InputRouter(new Sample_KeyboardReader());
        _clock = new GameClock();
        _clock.Initialize(_hub, _services);   // IGameClock を貸し出し、時間3命令の処理者になる

        // 2. マスターデータ（ステージ仕様の供給源。07_Data）
        var catalog = Sample_MasterCatalog.Build(
            new Seed.Hub.Contracts.CharacterId(1), new Seed.Hub.Contracts.CharacterId(2));

        // 3. カメラ・ライト（フェーズをまたいで使い回す）
        BuildCameraAndLight();

        // 4. フローとフェーズ（フェーズの追加＝AddPhase 1行）
        _flow = new GameFlow(_hub);
        _flow.AddPhase(new Sample_HomePhase(_hub, _input, catalog));
        _flow.AddPhase(new Sample_BattlePhase(_hub, _services, _input, catalog));
        _flow.AddPhase(new Sample_ShopPhase(_hub, _input));
        _flow.Start(Sample_PhaseIds.Home);    // Home = new PhaseId(1)
    }

    private void Update()
    {
        _clock.Tick(Time.deltaTime);          // dt の供給源はここだけ
        _input.Tick();                        // 入力のエッジ検出は毎フレーム1回
        _flow.Tick(_clock.UnscaledDelta);     // フェーズ遷移とメニューはポーズ中も動く
    }

    private void LateUpdate()
    {
        _hub.Pump();                          // PublishDeferred の遅延メッセージを配達（02_Hub）
    }

    private void OnDestroy()
    {
        _flow?.Dispose();                     // 滞在フェーズの OnExit まで実行
        _clock?.Dispose();
    }
}
```

遷移をログで観察したいときは、`Start()` の末尾に次を足します。

```csharp
_hub.Subscribe<PhaseChanged>(e =>
    Debug.Log($"{e.Previous} -> {e.Current} (payload={e.Payload})")); // 初回は Phase#0 -> Phase#1
```

### シーンアセットを使うステージ — `CreateLoadOperation`

デモの各フェーズはプリミティブ生成だけなので `CreateLoadOperation` を使っていません（null 返し=即時遷移）。
ステージが .unity アセットになったら、ここが差し込み口になります。

```csharp
public sealed class DungeonPhase : GamePhase
{
    private readonly ISceneLoader _loader;   // 合成ルートが UnitySceneLoader を注入。
                                             // 純C#テストでは偽実装に差し替える

    public DungeonPhase(ISceneLoader loader) { _loader = loader; }

    public override PhaseId Id => new PhaseId(4);

    public override IFlowOperation CreateLoadOperation(int payload)
    {
        return _loader.LoadScene("Dungeon"); // additive: true で現シーンに重ねることも可能
    }

    public override void OnEnter(int payload)
    {
        // ここに来た時点でロードは完了済み（GameFlow が IsDone まで待ってくれる）
    }
}
```

> 📖 **用語 — AsyncOperation**: Unity の非同期処理ハンドル。`SceneManager.LoadSceneAsync` 等が返し、
> `isDone` / `progress` で進捗を読めます。`UnitySceneLoader` はこれを `IFlowOperation` に包むだけの薄い層です。

> 📖 **用語 — additive ロード**: 現在のシーンを破棄せず、上に重ねてシーンを読み込む方式
> （`LoadSceneMode.Additive`）。`ISceneLoader.UnloadScene` が重ねた分を剥がす対になります。

## 5. 仕組み

`GameFlow.Tick(deltaTime)` の中身は、毎フレーム次の3段を上から順に通るだけです
（`Assets/Script/Flow/Runtime/GameFlow.cs`）。

1. **入場待ちの解決** — `_entering` がいれば `_loading.IsDone` を確認。未完了なら即 return
   （ロード中はどのフェーズも `Tick` されない）。完了していれば `OnEnter(payload)` →
   `PhaseChanged` 発行 → そのまま3段目へ進み、新フェーズの初回 `Tick` まで同じ呼び出しで済ませる
2. **予約の開始** — `_pending` があれば取り出し、現フェーズの `OnExit()` →
   次フェーズの `CreateLoadOperation(payload)` を呼んで return（退場フレームは誰も `Tick` されない）
3. **滞在フェーズの駆動** — `_current?.Tick(deltaTime)`

> 📖 **用語 — ポーリング**: 完了通知を待つのではなく、毎フレーム「終わった？」と自分から確認しに行く方式。
> `IFlowOperation` がコルーチンや `Task` に依存しないのは、純C#テストで遷移を1Tickずつ検証するためです。

設計の「なぜ」を3点補足します。

- **要求が予約止まりなのは、再入の安全のため**。`ChangePhaseCommand` の購読コールバックは
  `RequestChange` で `_pending` に書くだけです。フェーズ自身の `Tick` 中に遷移要求が届いても、
  実行中のフェーズがスタック上で破棄される事故（自分の足場を壊す）が構造的に起きません。
  これは三大規約の「状態は Tick、艶は Update」の実践でもあります——状態が変わる瞬間は必ず
  `Tick` の中にあり、購読コールバック（いつ呼ばれるか分からない場所）では状態を変えません
- **latest wins** — 遷移中（ロード待ち）に届いた要求は `_pending` に最新1件だけ保持され、
  入場完了後に処理されます。古い要求は新しい要求で上書きされます
- **単独処理者と方針の分離** — `ChangePhaseCommand` の処理者は `GameFlow` ただ1つ
  （三大規約「命令の処理者は1基盤」）。一方で「どんなフェーズがあるか」「PhaseId に何番を割るか」
  「payload の意味」はすべてアプリ側の知識です（三大規約「方針は App」——`Sample_PhaseIds` や
  `Sample_MasterCatalog` が App 層にあるのはこのため）。Flow 基盤は遷移の力学だけを知っています

> 📖 **用語 — latest wins**: 複数の要求が競合したとき、最後に届いた1件だけを採用する方針。
> キュー（全件順番に実行）と違い、「連打したら遷移が何往復もする」事故が起きません。

補助プロパティも仕組みと直結しています。`Current` は遷移中 `PhaseId.None` を返し、
`IsTransitioning` は「入場待ちまたは予約あり」、`LoadProgress` は進行中ロードの 0〜1
（遷移中でなければ 1）です。ローディング画面はこの2つを読むだけで作れます。

同一フェーズ再入が「完全な Exit→Enter」なのは意図的な仕様です。フェーズ内の状態を毎回ゼロから
作り直すことで、「前のステージの敵が残っていた」類のバグを設計段階で消しています。その代償として
**フェーズに置いた状態は再入のたび消える**ので、持ち越したい状態（所持金・編成など）は
永続ルートが所有するサービス（`ServiceRegistry` 登録）に置くのが規約です。

### UniTask でロードを書く（UniTaskFlowOperation）

ロード処理を async/await で書き、`IFlowOperation` に包んで返せます（→ [18_Libraries.md](18_Libraries.md)）。

```csharp
using Cysharp.Threading.Tasks;
using Seed.Flow;

protected override IFlowOperation CreateLoadOperation(int payload)
{
    // async 関数をそのまま遷移の非同期作業にできる（GameFlow 側の契約は不変）
    return new UniTaskFlowOperation(LoadStageAsync(payload));
}

private async UniTask LoadStageAsync(int payload)
{
    await UniTask.Delay(100);          // 例: セーブ読込・アセット先読みなど
    // await _assetLoader.LoadAsync<GameObject>("Stage201");  // 実戦ではこう繋がる
}
```

`async` を書いてよいのは「殻」（ロード・遷移・IO・演出）だけ、という規約は 18 章の依存の鉄則を参照してください。

## 6. よくあるつまずき

- **症状**: `Start()` を呼んだのに何も表示されない
  → **原因**: 入場は次の `Tick`。そもそも毎フレーム `flow.Tick(dt)` を呼んでいない
  → **対処**: 永続ルートの Update で `flow.Tick(clock.UnscaledDelta)` を必ず呼ぶ
- **症状**: `HubException`「〜は未登録のフェーズ」
  → **原因**: `AddPhase` 漏れ、または `PhaseId` の打ち間違い
  → **対処**: Runner の `AddPhase` 一覧と `Sample_PhaseIds` の発番を突き合わせる
- **症状**: `HubException`「〜は登録済み」
  → **原因**: 同一 `PhaseId` の `AddPhase` 二重登録（ID 定数の重複発番が典型）
  → **対処**: ID 定数クラスで発番を一元管理する（0 は `PhaseId.None` 予約なので 1 以上）
- **症状**: `ChangePhaseCommand` を発行したら処理者不在の `HubException`
  → **原因**: `GameFlow` を生成する前に命令を発行した
  → **対処**: 永続ルートで `new GameFlow(hub)` を済ませてから発行する
- **症状**: ポーズ中にメニューで遷移できない
  → **原因**: `flow.Tick` に `ScaledDelta` を渡している（ポーズ中は 0 になる値。[03_Clock.md](03_Clock.md)）
  → **対処**: `GameFlow` は `UnscaledDelta` で回す
- **症状**: ステージを切り替えたらフェーズ内に置いた状態（スコア等）が消えた
  → **原因**: 仕様。同一フェーズ再入でも OnExit→OnEnter が完全に回る
  → **対処**: 持ち越したい状態は永続ルート所有のサービス（`ServiceRegistry` 登録）へ移す
- **症状**: 遷移要求を連打したのに最後の行き先にしか行かない
  → **原因**: 仕様（latest wins）。遷移中の要求は最新1件だけ保持される
  → **対処**: 全要求を順に実行したい場合はアプリ側でキューイングを設計する（基盤は面倒を見ない）
- **症状**: シーンロード中、画面が固まって見える
  → **原因**: ロード中はどのフェーズも `Tick` されない仕様
  → **対処**: 永続ルート側の UI が `flow.IsTransitioning` / `flow.LoadProgress` を読んでローディング表示を出す
- **症状**: 戦闘に入ったが意図しないステージ（草原）になる
  → **原因**: payload が 0 または未登録の StageId。`Sample_BattlePhase.ResolveStage` が Stage1 に
  フォールバックする実装（`payload != 0 && TryGet` に失敗すると既定ステージ）
  → **対処**: `ChangePhaseCommand` の payload に正しい `StageId.Value` を渡す

## 7. 増やす・拡張する

- **フェーズを増やす（3手順）**
  1. `GamePhase` を継承したクラスを作る（`Id` / `OnEnter` / `OnExit` / `Tick` を override）
  2. `Assets/Script/App/Samples/Sample_PhaseIds.cs`（相当のアプリ定数クラス）に `PhaseId` を発番する
     （1以上。0 は `PhaseId.None` 予約）
  3. Runner の `Start()` に `_flow.AddPhase(new MyNewPhase(...))` を1行足す
- **ステージを増やす**: `Sample_MasterCatalog` に StageId 定数と `Sample_StageSpec` を追加し、
  ホームのメニューに `ChangePhaseCommand(Battle, StageId.Value)` の分岐を足す（[07_Data.md](07_Data.md)）
- **ステージをシーンアセット化する**: フェーズの `CreateLoadOperation(payload)` を override して
  `ISceneLoader.LoadScene(sceneName, additive)` の戻り値を返す。本番実装は `UnitySceneLoader`、
  純C#テストでは偽実装を注入する（本章 4節の `DungeonPhase` 例）
- **ローディング画面**: UI が `flow.IsTransitioning` と `flow.LoadProgress` を毎フレーム読む
- **BGM 切替・解析ログ**: `PhaseChanged` 通知（Previous / Current / Payload）を購読する。
  通知は新フェーズの `OnEnter` 完了後に飛ぶので、購読側は「もう入場済み」を前提にしてよい

## 8. 関連ファイルとテスト

- `Assets/Script/Flow/Runtime/GameFlow.cs` — 状態機械本体（この章の主役）
- `Assets/Script/Flow/Runtime/GamePhase.cs` — フェーズ基底クラス
- `Assets/Script/Flow/Runtime/IFlowOperation.cs` — 進捗契約と `CompletedFlowOperation`
- `Assets/Script/Flow/Runtime/ISceneLoader.cs` / `UnitySceneLoader.cs` — シーンロード抽象と本番実装
- `Assets/Script/Hub/Contracts/PhaseId.cs` — フェーズ共有ID（0 = None 予約）
- `Assets/Script/Hub/Contracts/Messages/ChangePhaseCommand.cs` — 遷移命令と `PhaseChanged` 通知
- `Assets/Script/App/Samples/Sample_GameFlowRunner.cs` — 永続ルート（デモの入口）
- `Assets/Script/App/Samples/Sample_PhaseIds.cs` / `Sample_HomePhase.cs` / `Sample_BattlePhase.cs` / `Sample_ShopPhase.cs` — アプリ側のフェーズ実装
- テスト: `Assets/Script/Flow/Tests/Editor/GameFlowTests.cs` —
  「次 Tick で入場」「同一フェーズ再入で完全な Exit→Enter」「ロード完了まで Tick 停止」
  「フェーズ Tick 中の要求は遅延」「未登録IDは例外」「Dispose で退場」を純C#で検証（[17_Tests.md](17_Tests.md)）

[← 前: 03_Clock](03_Clock.md) | [索引](00_Roadmap.md) | [次: 05_Input →](05_Input.md)
