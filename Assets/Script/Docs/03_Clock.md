# 03. Seed.Clock — ゲーム時間の供給源（ポーズ・倍速・ヒットストップ）

[← 前: 02_Hub](02_Hub.md) | [索引](README.md) | [次: 04_Flow →](04_Flow.md)

## この章で分かること

- ゲーム内の時間を「実時間」と「ゲーム時間」に分ける理由と、その分岐点が `GameClock` の1箇所である仕組み
- **ScaledDelta**（ゲーム進行用 dt）と **UnscaledDelta**（実時間 dt）の使い分け——どの系をどちらで回すか
- ポーズ・スローモーション・倍速・ヒットストップを命令1発で全基盤に効かせる方法
- ポーズとヒットストップが重なったときの正確な仕様（凍結・再開後消化・長い方優先）
- 時間演出の命令を自分で増やす手順

## 前提

先に読んでおく章と、この章で使う知識は次のとおりです。

- [01_Demo](01_Demo.md) — デモシーンの起動方法と操作の全体像
- [02_Hub](02_Hub.md) — `MessageHub` の命令（`PublishCommand` / `SubscribeCommand<T>` は処理者1人を強制）、`ServiceRegistry`（`Register<T>` / `Resolve<T>` で窓口を貸し借り）、`HubException`、`SubscriptionBag`

この章が初出の主な用語: dt（デルタタイム）、実 dt とゲーム dt、`ScaledDelta` / `UnscaledDelta`、時間倍率（`Scale`）、ヒットストップ、`Time.timeScale`。

> 📖 **用語 — dt（デルタタイム）**: 前のフレームから今のフレームまでに経過した時間（秒）。Unity では `Time.deltaTime` で取れます。「移動速度 × dt」のように掛けることで、フレームレートが変動しても実時間ベースで同じ速さの動きになります。

## 1. これは何か

`GameClock` は、Unity が渡してくる実時間の dt を **ゲーム用の dt に加工して配る、たった1つの蛇口** です。毎フレーム最初に `Tick(実dt)` を呼ぶと、その 1 フレームぶんの

- **`ScaledDelta`** — ゲーム進行用の dt。ポーズ中・ヒットストップ中は 0、倍率（`Scale`）で伸縮
- **`UnscaledDelta`** — 実時間の dt。常に実 dt がそのまま入る

の2つが確定します。ゲーム進行を担う系はすべて `ScaledDelta` を受け取って動くため、この1箇所で dt を 0 にすればポーズが、0.1 倍にすればスローモーションが、**キャラ・AI・フロー・再生キューの全基盤へ同時に**効きます。

これが無いとどうなるか。各システムが `Time.deltaTime` を直接読むと、ポーズを実装するために「全システムに `if (paused) return;` を書いて回る」ことになり、1箇所でも書き漏らすと「ポーズ中なのに敵だけ動く」バグになります。Unity 標準の `Time.timeScale` を使う手もありますが、それは物理・`Animator`・パーティクルまでエンジン全体を巻き込むグローバル変数で、「ポーズ中もメニューの UI アニメは動かしたい」といった選択的な制御が難しくなります。Seed では dt の供給源を `GameClock` に一本化し、`Time.timeScale` には触りません。

> 📖 **用語 — Time.timeScale**: Unity エンジン全体の時間進行倍率。0 にすると物理演算や `Time.deltaTime` 自体が止まる強力なグローバル設定ですが、影響範囲が広すぎて選択的な制御（UI だけ動かす等）に向きません。本プロジェクトでは使いません。

もうひとつの動機は **テスト容易性と決定性** です。`GameClock` は純 C#（`MonoBehaviour` ではない）で、`Tick(0.016f)` のように好きな dt を手渡しできるため、ポーズやヒットストップの仕様をエディタ再生なしの EditMode テストで検証できます。さらに [12_GameCore](12_GameCore.md) のロジック時間（`AdvanceTime`）は `ScaledDelta` 由来のミリ秒で進むため、ポーズはリプレイ上「時間が進まなかった」として自然に記録されます。

> 📖 **用語 — 純C#**: `MonoBehaviour` や `GameObject` に依存しない、`new` で作れる普通の C# クラスのこと。`Seed.Clock` の asmdef は `noEngineReferences: true` で、`UnityEngine` への参照自体を禁止しています。

## 2. 全体像

### 部品表

| 部品 | 場所 | 役割 |
|---|---|---|
| `GameClock` | `Assets/Script/Clock/Runtime/GameClock.cs` | 本体。dt の加工と3命令の唯一の処理者（純C#・`sealed`・`IDisposable`） |
| `IGameClock` | `Assets/Script/Hub/Contracts/IGameClock.cs` | 読み取り窓口（`Scale` / `IsPaused` / `ScaledDelta` / `UnscaledDelta`） |
| `SetPausedCommand` | 同上 | ポーズ切替の命令（`bool IsPaused`） |
| `SetTimeScaleCommand` | 同上 | 時間倍率変更の命令（`float Scale`、0以上） |
| `HitStopCommand` | 同上 | ヒットストップの命令（`float Seconds`、実時間） |
| テスト | `Assets/Script/Clock/Tests/Editor/GameClockTests.cs` | 仕様の検証6本（純C#） |

3つの命令はすべて `ICommandMessage` を実装した `readonly struct` です。

> 📖 **用語 — readonly struct**: 生成後に中身を書き換えられない構造体。命令メッセージを値型かつ不変にすることで、GC 割り当てなしで安全に受け渡せます。

### データの流れ

```
Time.deltaTime（実dt）
        │  毎フレーム最初に
        ▼
GameClock.Tick(実dt) ─────▶ UnscaledDelta = 実dt   … 常に素通し
        │
        ├─ ① IsPaused ?            → ScaledDelta = 0（ヒットストップ残量は凍結）
        ├─ ② ヒットストップ残量 > 0 ? → ScaledDelta = 0（残量を実dtで消化）
        └─ ③ それ以外              → ScaledDelta = 実dt × Scale
```

操作の流れは Hub 経由の一方通行です。

```
どこかの基盤  ── hub.PublishCommand(SetPausedCommand 等) ──▶  GameClock（唯一の処理者）
どこかの基盤  ── services.Resolve<IGameClock>() ────────▶  読み取り（IsPaused 等）
```

### ScaledDelta / UnscaledDelta の使い分け（デモの実際の配分）

| 系 | 使う dt | 理由 |
|---|---|---|
| 戦闘パイプライン（キャラ・AI・ダメージ・弾） | `ScaledDelta` | ポーズ・ヒットストップで止まるべき「ゲームの進行」だから |
| `GameFlow`（フェーズ遷移） | `UnscaledDelta` | ポーズ中でも「[B] でホームへ戻る」を効かせるため |
| UI（テキストパネル・メニュー） | `UnscaledDelta` | ポーズ画面の表示・アニメが凍り付かないようにするため |

判断基準は「**ポーズしたとき、それは止まるべきか？**」の一問です。止まるべきなら `ScaledDelta`、止まってはいけない（ポーズメニュー、画面遷移、実時間の演出）なら `UnscaledDelta`。ただしこの配分は基盤側では決めず、アプリ（App 層）が決めます——三大規約の「方針は App」です（詳細は 5 節）。

## 3. 動かして試す

デモ（[01_Demo](01_Demo.md) と同じもの）で時間基盤の挙動を確認します。

1. **File > New Scene** で新規シーンを作成（カメラ・ライトは不要。無ければ Runner が自動生成します）
2. Hierarchy ビューで右クリック → **Create Empty** で空の GameObject を作成
3. Inspector の **Add Component** で「Sample_GameFlowRunner」を検索して追加（Inspector の設定項目はありません）
4. **Play** を押す

Play 直後、Game ビューにホームのテキストパネル（Title「ホーム」と [1]〜[5] のメニュー行）が表示され、Hierarchy に「HomePhase」GameObject が生えます。Console にエラーは出ません。ここから時間基盤の3機能を順に試します。

5. **[1] キー**で草原（StageId 201）へ出撃。Hierarchy の HomePhase が消え「Stage_Grassland」のルートが生成されます
6. **[P] キー**でポーズ。戦闘の進行（キャラ・敵の動き）がその場で静止します。内部では `SetPausedCommand(!_clock.IsPaused)` が発行され、戦闘パイプライン（`ScaledDelta` 駆動）に渡る dt が 0 になっています
7. ポーズ中に **[B] キー**を押すとホームへ戻れます。`GameFlow` は `UnscaledDelta` で回っているため、ポーズはフェーズ遷移を止めません
8. 再度出撃し、敵の攻撃を**わざと受けて**みます。被弾の瞬間、世界全体が 0.06 秒だけ静止します。これは被弾処理が `HitStopCommand(0.06f)` を発行した結果です（キー操作ではなく自動）
9. スロー/倍速（`SetTimeScaleCommand`）はデモにキー割当がありません（命令自体は実装済み。7 節でキーに割り当てる例を示します）

> 📖 **用語 — ヒットストップ**: 打撃がヒットした瞬間にゲーム時間を数フレームだけ止める演出技法。「手応え」を強調するためにアクションゲームで広く使われます。実時間指定（例: 0.06 秒）で、その間 `ScaledDelta` が 0 になります。

## 4. コードで使う

### 最小例（純C#だけで完結）

```csharp
using Seed.Clock;
using Seed.Hub;
using Seed.Hub.Contracts;

var hub = new MessageHub();
var services = new ServiceRegistry();

var clock = new GameClock();
clock.Initialize(hub, services); // IGameClock を貸し出し、3命令の唯一の処理者になる

// 毎フレーム最初に呼ぶ（dt の加工が何より先）
clock.Tick(0.016f);              // Unity 上なら Time.deltaTime を渡す
float gameDt = clock.ScaledDelta;    // ゲーム進行はこちら（今は等速なので 0.016）
float realDt = clock.UnscaledDelta;  // 実時間はこちら（常に 0.016）

// 操作はすべて命令1発（どの基盤からでも Hub 経由で届く）
hub.PublishCommand(new SetPausedCommand(true));     // ポーズ
hub.PublishCommand(new SetTimeScaleCommand(0.1f));  // スローモーション
hub.PublishCommand(new HitStopCommand(0.06f));      // ヒットストップ 0.06 秒

// 読み取りは ServiceRegistry から窓口を借りる
IGameClock view = services.Resolve<IGameClock>();
bool paused = view.IsPaused;     // true

// 終了時: 購読を解除し、IGameClock を台帳から返却する
clock.Dispose();
```

### 実戦例（永続ルートでの配線）

`Sample_GameFlowRunner`（`Assets/Script/App/Samples/Sample_GameFlowRunner.cs`）が本番の型です。ポイントは **`Update` の先頭で `clock.Tick` を呼び、後続の全 Tick に加工済み dt を配る**こと。

> 📖 **用語 — 永続ルート**: フェーズ（ホーム・戦闘・ショップ）をまたいで生き続ける唯一の `MonoBehaviour`。`MessageHub` / `ServiceRegistry` / `GameClock` / `GameFlow` など全基盤を所有し、`Update` で各基盤の Tick を正しい順序で呼びます。

```csharp
public sealed class GameRoot : MonoBehaviour
{
    private MessageHub _hub;
    private ServiceRegistry _services;
    private GameClock _clock;
    private GameFlow _flow;

    private void Start()
    {
        _hub = new MessageHub();
        _services = new ServiceRegistry();
        _clock = new GameClock();
        _clock.Initialize(_hub, _services); // ここで IGameClock が借りられるようになる

        _flow = new GameFlow(_hub);
        // _flow.AddPhase(...) は 04_Flow 参照
    }

    private void Update()
    {
        _clock.Tick(Time.deltaTime);      // ① dt の加工が何より先
        _flow.Tick(_clock.UnscaledDelta); // ② フローはポーズ中も動かす（実時間）
    }

    private void OnDestroy()
    {
        _flow?.Dispose();
        _clock?.Dispose(); // 購読解除 + IGameClock の Unregister
    }
}
```

戦闘フェーズ側（`Sample_BattlePhase`）は窓口を借りて読むだけです。

```csharp
// OnEnter で窓口を借りる（所有はしない。所有者は永続ルート）
_clock = _services.Resolve<IGameClock>();

// 被弾の瞬間: 命令1発で世界全体が 0.06 秒止まる
_hub.PublishCommand(new HitStopCommand(0.06f));

// [P] キー: 現在値を読んで反転を命令する
_hub.PublishCommand(new SetPausedCommand(!_clock.IsPaused));

// 毎 Tick: 戦闘の進行はゲーム dt で回す
if (!_isOver && !_clock.IsPaused)
{
    _pipeline.Tick(_clock.ScaledDelta); // ポーズ中は ScaledDelta=0 だが、
}                                       // デモは 0 秒 Tick の空回し自体も省いている
```

## 5. 仕組み

`GameClock.Tick(rawDeltaSeconds)` の中身は 15 行ほどで、優先順位が仕様のすべてです。

1. **`UnscaledDelta` は無条件に実 dt を素通し**。ポーズ中も倍速中も、実時間は常に流れます
2. **ポーズ判定が最優先**。`IsPaused` なら `ScaledDelta = 0` で即 return。このとき**ヒットストップの残量には触らない**ため、ポーズ中はヒットストップも凍結され、ポーズ解除後に残りが消化されます
3. **次にヒットストップ**。残量が正なら `ScaledDelta = 0` にし、残量から**実 dt**（倍率の影響を受けない）を引きます。判定が減算より先なので、残量を使い切るフレーム自体も `ScaledDelta = 0` のままで、次のフレームから動き出します（テスト `HitStop_FreezesForDuration` がこの境界を検証）
4. **最後に倍率**。`ScaledDelta = 実dt × Scale`。`Scale` の初期値は 1（等速）です

ヒットストップの重複要求は `OnHitStop` で「新しい要求が残量より長いときだけ上書き」します。つまり**長い方が残り、加算はしません**。連続ヒットで停止時間が雪だるま式に伸びるのを防ぐ仕様です。倍率の負値は `HubException` を投げます——0 に黙って丸めると構成ミスが隠れるため、あえて例外にしています（`Scale = 0` 自体は合法で、出力上はポーズと同義の停止です。ただし `IsPaused` は false のままで、ヒットストップの残量消化も止まりません）。

三大規約との関係はこの章の核心です。

- **「状態は Tick、艶は Update」**: ゲームの状態を進める処理はすべて「dt を受け取る Tick」で書かれています。だからこそ、渡す dt を `GameClock` の1箇所で加工するだけで、全基盤のポーズ・倍速が成立します。もし状態が `Update` の中で `Time.deltaTime` を直読みしていたら、この仕組みは破綻します
- **「方針は App」**: どの系を `ScaledDelta` / `UnscaledDelta` で回すかは基盤側では決めず、App 層が配線で決めます（デモでは Runner が `GameFlow` に `UnscaledDelta` を、戦闘フェーズがパイプラインに `ScaledDelta` を渡している）。基盤は2つの dt を供給するだけです
- **「命令の処理者は1基盤」**: `SetPausedCommand` / `SetTimeScaleCommand` / `HitStopCommand` の処理者は `GameClock` ただ1つ。`SubscribeCommand<T>` が二重処理者を `HubException` で拒否するため、「誰がポーズを反映するのか」が構造的に一意になります

ライフサイクルは `Initialize` と `Dispose` の対です。`Initialize(hub, services)` で `ServiceRegistry` に `IGameClock` を `Register` し、3命令の購読を `SubscriptionBag`（容量 3）に溜めます。`Dispose` で購読をまとめて解除し、`IGameClock` を `Unregister` します。`Dispose` 後に命令を発行すると「処理者不在」の `HubException` になります（握り潰さず検知する方針。テスト `Lifecycle_AndValidation` 参照）。

> 📖 **用語 — asmdef / noEngineReferences**: asmdef（Assembly Definition）はコードをアセンブリ単位に分割する Unity の仕組み。`Seed.Clock.asmdef` は参照先を `Seed.Hub` と `Seed.Hub.Contracts` に限定し、`noEngineReferences: true` で `UnityEngine` 参照を禁止しています。時間基盤がエンジン非依存であることをコンパイラレベルで保証する設定です。

## 6. よくあるつまずき

**症状: 命令を発行したら `HubException`（処理者がいない）**
→ 原因: `GameClock` を生成していない、`Initialize` を呼んでいない、または既に `Dispose` 済み。
→ 対処: 永続ルートの `Start` で `new GameClock()` → `Initialize(hub, services)` を済ませてから命令を発行します。

**症状: `SetTimeScaleCommand` で `HubException`（時間倍率に負値が指定された）**
→ 原因: 負の倍率は構成ミスとして例外になる仕様（0 に丸めません）。
→ 対処: 停止したいなら `SetTimeScaleCommand(0f)` か `SetPausedCommand(true)` を使います。

**症状: ポーズしたら UI やフェーズ遷移まで止まった**
→ 原因: その系を `ScaledDelta` で回している。
→ 対処: 「ポーズ中も動くべきもの」（メニュー、`GameFlow`、ポーズ画面の演出）は `UnscaledDelta` で回します。2 節の表を参照。

**症状: ポーズを解除した直後、少しの間キャラが動かない**
→ 原因ではなく仕様: ポーズ中はヒットストップの残量が凍結され、解除後に残りを消化します。被弾直後にポーズ→解除すると、残っていたヒットストップぶんだけ静止してから動き出します。

**症状: 連続ヒットさせてもヒットストップが長くならない**
→ 仕様: 重複要求は長い方が残るだけで加算しません。長く止めたければ最初から長い `Seconds` を1発指定します。

**症状: あるシステムだけ 1 フレーム古い dt で動く / ポーズが 1 フレーム遅れる**
→ 原因: `clock.Tick` より前にそのシステムの Tick を呼んでいる。
→ 対処: 永続ルートの `Update` で `_clock.Tick(Time.deltaTime)` を**必ず先頭**に置き、加工後の dt を後続へ配ります。

**症状: `services.Resolve<IGameClock>()` が失敗する**
→ 原因: `Initialize` 前に借りようとした（フェーズの生成順が早すぎる）、または `Dispose` 後。
→ 対処: 永続ルートで `GameClock` を先に配線してからフェーズを起動します。デモの `Sample_GameFlowRunner.Start` の順序が手本です。

## 7. 増やす・拡張する

### 時間演出の命令を追加する（例: 一定時間だけスローになる SlowMoCommand）

1. `Assets/Script/Hub/Contracts/IGameClock.cs` に命令を追加します（契約は Hub/Contracts に置く規約）

```csharp
/// <summary>指定実時間だけスローにする命令（処理者は時間基盤のみ）。</summary>
public readonly struct SlowMoCommand : ICommandMessage
{
    /// <summary>スロー中の倍率。</summary>
    public readonly float Scale;

    /// <summary>継続する実時間（秒）。</summary>
    public readonly float Seconds;

    /// <summary>SlowMoCommand を生成する。</summary>
    public SlowMoCommand(float scale, float seconds)
    {
        Scale = scale;
        Seconds = seconds;
    }
}
```

2. `Assets/Script/Clock/Runtime/GameClock.cs` の `Initialize` に購読を1行足します（`SubscriptionBag` の容量指定も購読数に合わせて 3 → 4 に）

```csharp
hub.SubscribeCommand<SlowMoCommand>(OnSlowMo).AddTo(_subscriptions);
```

3. `GameClock` に残り時間フィールドと `Tick` 内の消化ロジックを足します（ヒットストップの実装が手本。ポーズ中に凍結するか・重複時にどちらが勝つかを最初に決めておくこと）
4. `Assets/Script/Clock/Tests/Editor/GameClockTests.cs` に境界のテストを足します（発動中・消化しきるフレーム・重複・ポーズとの相互作用）

### デモでスロー/倍速をキーに割り当てて試す

`SetTimeScaleCommand` は実装済みでキーが無いだけなので、`Sample_BattlePhase` の入力処理（[P] の `SetPausedCommand` 発行と同じ場所）に1行足せば試せます。

```csharp
_hub.PublishCommand(new SetTimeScaleCommand(0.1f)); // 任意のキーでスローモーション
```

### 「ポーズしても動く演出」を作る

新しい演出システムを作るときは、Tick の引数をどちらの dt にするかを配線側（App 層）で決めます。戦闘の一部なら `ScaledDelta`、ポーズメニューや画面遷移の演出なら `UnscaledDelta` です。基盤のコードに `if (paused)` を書き始めたら設計を疑ってください——ポーズの表現は dt=0 がすでに担っています。

## 8. 関連ファイルとテスト

- `Assets/Script/Clock/Runtime/GameClock.cs` — 本体（純C#、約100行）
- `Assets/Script/Clock/Runtime/Seed.Clock.asmdef` — `noEngineReferences: true` のアセンブリ定義
- `Assets/Script/Hub/Contracts/IGameClock.cs` — 読み取り窓口 `IGameClock` と3命令（`SetPausedCommand` / `SetTimeScaleCommand` / `HitStopCommand`）
- `Assets/Script/Clock/Tests/Editor/GameClockTests.cs` — 仕様の検証6本
- `Assets/Script/App/Samples/Sample_GameFlowRunner.cs` — 永続ルートでの配線例（`Update` 先頭で `Tick`）
- `Assets/Script/App/Samples/Sample_BattlePhase.cs` — 窓口の借用・`ScaledDelta` 駆動・ヒットストップ発行の実例

テストは **Window > General > Test Runner** の **EditMode** タブから実行します。`GameClockTests` の6本（等速素通し・倍率伸縮・ポーズ・ヒットストップ消化・重複は長い方・ライフサイクルと負値検証）がすべて緑になることを確認してください。エンジン非依存の純C#なので、エディタ再生なしで一瞬で終わります。

> 📖 **用語 — EditMode テスト**: Unity Test Runner のうち、エディタの再生（Play）を伴わずに実行されるテスト。`MonoBehaviour` に依存しない純C#のコードなら、通常の NUnit テストと同じ速度で回せます。

[← 前: 02_Hub](02_Hub.md) | [索引](README.md) | [次: 04_Flow →](04_Flow.md)
