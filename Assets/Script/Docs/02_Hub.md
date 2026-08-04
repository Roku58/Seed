# 02. Seed.Hub — 基盤同士が互いを知らずに会話するためのメッセージ基盤

[← 前: 01_Demo](01_Demo.md) | [索引](README.md) | [次: 03_Clock →](03_Clock.md)

## この章で分かること

- 基盤間の会話手段は3経路 — **通知**（Publish/Subscribe）・**命令**（PublishCommand/SubscribeCommand）・**問い合わせ**（ServiceRegistry）— で、それぞれいつ使うか
- `MessageHub` の使い方（購読・発行・遅延発行 PublishDeferred と Pump）と、規約違反が即例外になる理由
- 購読の後始末を1行で済ませる `SubscriptionBag` の書き味
- 依頼-応答を混線させない `RequestId` 相関の標準パターン
- エディタウィンドウ **Seed/Message Tracer** でメッセージフローを観測する方法

## 前提

- [01_Demo.md](01_Demo.md) — 統合デモを一度動かしていること。デモの起点 `Sample_GameFlowRunner` が、この章で言う「合成ルート」の実例です
- Unity エディタの基本操作（空 GameObject の作成、Add Component、Play）

> 📖 **用語 — 合成ルート（永続ルート）**: アプリ起動時に各基盤を new して配線する唯一の場所のこと。
> Seed では `Sample_GameFlowRunner`（`Assets/Script/App/Samples/Sample_GameFlowRunner.cs`）がその実例で、
> `MessageHub` や `ServiceRegistry` もここで生成されます。シーンをまたいで生き続けるため「永続ルート」とも呼びます。

この章が初出の主な用語: 通知 / 命令 / 問い合わせ（ServiceRegistry）/ 遅延発行（PublishDeferred・Pump）/ 相関ID（RequestId）/ メッセージトレーサ。

## 1. これは何か

Seed は UI・キャラクター・フロー・永続化などの「基盤」を分けて作ります。基盤同士が互いのクラスを直接参照すると、
どれか1つを変えるたびに全部が壊れる密結合になります。`Seed.Hub.MessageHub` は、その代わりに
「メッセージ型」だけを共有して会話するための掲示板です。発行側は「誰が聞くか」を知らず、購読側は「誰が言ったか」を知りません。

無いと困ること:

- UI がキャラクター基盤の実装クラスを直接呼ぶ → キャラクター側のリファクタで UI がビルドエラーになる
- 「セーブして」の依頼を複数の基盤が勝手に処理 → 二重セーブなどの競合バグ
- どの基盤がいつ何を発行したか追えない → 「基盤同士が互いを知らない」設計では不具合調査の手掛かりが消える

MessageHub はこれらを「型で区別されたメッセージ＋ランタイム検査＋観測ウィンドウ」で解決します。

**GameCore の EventHub とは別物**です（→ [12_GameCore.md](12_GameCore.md)）。EventHub は決定的ロジックの内側で
リプレイの記録対象、MessageHub は Unity メインループ側で非決定でよい、という役割分担のため、
設計語彙（優先度・安定順序・発行中の解除/登録の遅延処理）は共有しつつ実装は意図的に分けています。
参照を共有すると GameCore が全基盤の依存先になってしまうためです。

> 📖 **用語 — 決定性**: 同じ入力を与えれば必ず同じ結果になる性質。リプレイやヘッドレス検証の前提。
> GameCore 側は決定性を守る必要がありますが、MessageHub が受け持つ UI・演出の連携は非決定で構いません。

## 2. 全体像

### 部品表

| 部品 | 場所 | 役割 |
|---|---|---|
| `MessageHub` | `Assets/Script/Hub/Runtime/MessageHub.cs` | 通知・命令の発行と配達（純C#、sealed class） |
| `SubscriptionBag` | `Assets/Script/Hub/Runtime/SubscriptionBag.cs` | 購読トークンを束ねて一括解除する袋 |
| `ServiceRegistry` | `Assets/Script/Hub/Runtime/ServiceRegistry.cs` | 同期問い合わせ窓口の台帳 |
| `RequestIdSource` | `Assets/Script/Hub/Runtime/RequestIdSource.cs` | 相関IDの発番器（1プロセス1個） |
| `INotificationMessage` / `ICommandMessage` | `Assets/Script/Hub/Contracts/MessageMarkers.cs` | 通知と命令を型で区別する印 |
| `RequestId` | `Assets/Script/Hub/Contracts/RequestId.cs` | 依頼-応答の相関ID（readonly struct） |
| メッセージ型 | `Assets/Script/Hub/Contracts/Messages/` | 境界を越える readonly struct（利用者が自作） |
| `MessageHubRegistry` | `Assets/Script/Hub/Runtime/MessageHubRegistry.cs` | 開発時専用の Hub 台帳（弱参照） |
| `MessageHubTracerWindow` | `Assets/Script/Hub/Editor/MessageHubTracerWindow.cs` | 観測用エディタウィンドウ |

### 3経路の使い分け

| 経路 | 型・API | 意味 | 受け手の数 |
|---|---|---|---|
| 通知 | `INotificationMessage` / `Publish` + `Subscribe<T>` | 「〜した」（過去形の出来事） | 0人でも成立・複数OK |
| 命令 | `ICommandMessage` / `PublishCommand` + `SubscribeCommand<T>` | 「〜してほしい」 | 必ず1基盤 |
| 問い合わせ | `ServiceRegistry` の `Register<T>` / `Resolve<T>` | 「今どうなっている?」 | 窓口1つを同期で読む |

使い分けの目安: **出来事の報告は通知**（撃破した・フェーズが変わった）、**依頼は命令**（セーブして・画面を出して）、
**毎フレームの連続値は問い合わせ**（位置・HPゲージ）。連続値をメッセージに流すと毎フレーム全購読者に配達が走るため、
「今の値を読むだけ」の用途は `ServiceRegistry` のインターフェース経由が規約です。

### データの流れ

```
発行側基盤            MessageHub                          受信側基盤
   │ Publish(通知) ────────▶ 購読者リスト（優先度順） ──▶ handler を N 件呼ぶ（0件でも成立）
   │ PublishCommand(命令) ─▶ 処理者（必ず1件） ────────▶ handler を 1 件呼ぶ（不在なら即例外）
   │ PublishDeferred(…) ──▶ 遅延キュー ──(フレーム末尾の Pump)──▶ 上と同じ経路で配達
   │
   │ ServiceRegistry.Resolve<IF>() ──▶ 登録済み実装を即返す（メッセージを介さない同期読み取り）
```

## 3. 動かして試す

`MessageHub` は純C#クラスなので、シーンに直接置くものはありません。最小の体験は自作スクリプト1本で作ります。

1. Project ビューで `Assets/Script/` を右クリック → **Create > C# Script** → 名前を `HubDemo` にする
2. 生成された `HubDemo.cs` を開き、下の「4. コードで使う > 最小例」の内容で丸ごと置き換えて保存する
3. **File > New Scene** で新しいシーンを作る（保存は任意）
4. **GameObject > Create Empty** で空オブジェクトを作り、Inspector の **Add Component** で `HubDemo` を付ける
5. メニュー **Seed > Message Tracer** を開く。Play 前なので
   「観測中の MessageHub がありません。Play を開始すると自動で接続されます。」という青い HelpBox が出ます
6. **Play** を押す

期待される結果:

- **Console**: Awake の時点で「撃破: 7」「セーブ実行」の2行。最初の LateUpdate の `Pump()` で「撃破: 8」が出ます
  （`PublishDeferred` した分は Pump まで配達されません）
- **Message Tracer**: `[X.XXs] Hub0  EnemyDefeated  → 1件配達` `[X.XXs] Hub0  SaveGameCommand  → 1件配達` のような行が
  新しい順に並び、上部の型別頻度に `EnemyDefeated ×2   SaveGameCommand ×1` と出ます
- **Hierarchy**: 変化なし（Hub はコンポーネントを生成しません）

### メッセージトレーサの画面

- **ツールバー左から**: 「記録」トグル（OFF にすると以降の発行を記録しない）/「型別頻度」トグル（頻度サマリの表示切替）/
  「クリア」ボタン（履歴と頻度を消去）。右端に `Hub: N  記録: N/256`（接続中の Hub 数と履歴件数）
- **型別頻度**: 発行回数の多い型を上位8件まで `型名 ×回数` の形で1行表示
- **履歴**: 直近256件を新しい順に表示。1行の書式は `[12.34s] Hub0  TypeName  → N件配達`
  （時刻はエディタ起動からの秒、Hub 番号は接続順）。購読者0人の発行も `→ 0件配達` として記録されます。
  配達中に購読者が例外を投げた行は `✕ 例外型: メッセージ` の**太字**になります
- 生きている全 `MessageHub` を**毎秒**再走査して自動接続します。Play 中に生成された Hub も自動で拾います
- 記録されるのは型名・購読者数・時刻・失敗文字列だけで、メッセージ本体は渡りません（ボックス化なし。
  観測イベントが未購読なら本番コストもゼロ）

> 📖 **用語 — ボックス化（boxing）**: struct を object や interface 型の変数に入れるときに起きるヒープ確保のこと。
> 毎フレーム発生すると GC 負荷になるため、トレーサはメッセージ本体を受け取らない設計にしています。

## 4. コードで使う

### 最小例

```csharp
using Seed.Hub;
using Seed.Hub.Contracts;
using UnityEngine;

/// <summary>通知（過去形・複数購読OK）。</summary>
public readonly struct EnemyDefeated : INotificationMessage
{
    public readonly int Id;
    public EnemyDefeated(int id) { Id = id; }
}

/// <summary>命令（処理者1基盤のみ）。</summary>
public readonly struct SaveGameCommand : ICommandMessage { }

public sealed class HubDemo : MonoBehaviour
{
    private readonly SubscriptionBag _bag = new SubscriptionBag();
    private MessageHub _hub;

    private void Awake()
    {
        _hub = new MessageHub();
        _hub.Subscribe<EnemyDefeated>(m => Debug.Log($"撃破: {m.Id}")).AddTo(_bag);
        _hub.SubscribeCommand<SaveGameCommand>(_ => Debug.Log("セーブ実行")).AddTo(_bag);
        _hub.Publish(new EnemyDefeated(7));         // 通知は Publish
        _hub.PublishCommand(new SaveGameCommand()); // 命令は PublishCommand
        _hub.PublishDeferred(new EnemyDefeated(8)); // 今の連鎖の外へ回す
    }

    private void LateUpdate() { _hub.Pump(); }      // フレーム末尾で遅延分を配達
    private void OnDestroy() { _bag.Dispose(); }    // 購読をまとめて解除
}
```

> 📖 **用語 — readonly struct**: 生成後に中身を書き換えられない構造体。メッセージは境界を越えて
> 複数の基盤へ渡るため、途中で書き換えられない値型に限定しています（`MessageHub` の型制約も `where TMessage : struct`）。

> 📖 **用語 — IDisposable**: 「後始末が必要なもの」を表す C# 標準インターフェース。`Subscribe<T>` の戻り値は
> 購読解除トークン（IDisposable）で、`Dispose()` すると購読が外れます。`AddTo(_bag)` はそれを袋に預ける糖衣です。

> 📖 **用語 — デリゲート**: メソッドを値として持ち運ぶ C# の仕組み。`Subscribe<T>` に渡す
> `Action<TMessage>`（引数1つ・戻り値なしのデリゲート型）が購読ハンドラの本体です。

### 実戦例1 — 依頼-応答（RequestId 相関）

「セーブして → 終わったら教えて」のような依頼-応答は、命令と通知を相関IDで対応付けるのが標準パターンです
（`RequestId.cs` の doc コメントに規約として明記）。複数の依頼が並行しても混線しません。

```csharp
using Seed.Hub;
using Seed.Hub.Contracts;
using UnityEngine;

/// <summary>セーブを依頼する命令（処理者は永続化基盤のみ）。</summary>
public readonly struct SaveRequested : ICommandMessage
{
    public readonly RequestId Id;                       // 相関ID（応答と対応付ける）
    public SaveRequested(RequestId id) { Id = id; }
}

/// <summary>セーブが完了したという通知（過去形・同じIDを載せ返す）。</summary>
public readonly struct SaveCompleted : INotificationMessage
{
    public readonly RequestId Id;
    public readonly bool Success;
    public SaveCompleted(RequestId id, bool success) { Id = id; Success = success; }
}

/// <summary>依頼側の例（UIパネルなど）。</summary>
public sealed class SavePanel : System.IDisposable
{
    private readonly MessageHub _hub;
    private readonly RequestIdSource _requestIds;       // 永続ルートから配られた発番器（1プロセス1個が規約）
    private readonly SubscriptionBag _bag = new SubscriptionBag();
    private RequestId _waiting = RequestId.None;        // None = 依頼なしの予約値

    public SavePanel(MessageHub hub, RequestIdSource requestIds)
    {
        _hub = hub;
        _requestIds = requestIds;
        _hub.Subscribe<SaveCompleted>(OnSaveCompleted).AddTo(_bag);
    }

    public void OnSaveButton()
    {
        _waiting = _requestIds.Next();                  // 単調増加で発番（重複なし）
        _hub.PublishCommand(new SaveRequested(_waiting));
    }

    private void OnSaveCompleted(SaveCompleted m)
    {
        if (!m.Id.Equals(_waiting)) { return; }         // 自分が発行したIDだけを拾う
        _waiting = RequestId.None;
        Debug.Log(m.Success ? "セーブ完了" : "セーブ失敗");
    }

    public void Dispose() { _bag.Dispose(); }
}
```

### 実戦例2 — 問い合わせ（ServiceRegistry）

毎フレームの連続値はメッセージに流さず、インターフェースの窓口を同期で読みます。

```csharp
// 合成ルート側: キャラクター基盤が実装した窓口を「インターフェース型で」貸し出す
_services.Register<ICharacterQuery>(characterQueryImpl);   // 実装型での登録は開発ビルドで HubException

// 借りる側: 実装クラスを知らずに「今」を読む
if (_services.TryResolve<ICharacterQuery>(out var query)
    && query.TryGetPosition(targetId, out var position))   // position は HubVector3（契約層専用の純C#ベクトル）
{
    // 毎フレーム呼んでよい（メッセージ配達が走らない同期読み取り）
}
```

`Resolve<TService>()` は未登録だと「合成ルートを確認」という HubException になります（配線漏れの早期発見）。
取得できないことが正常系なら `TryResolve` を使います。窓口が不要になったら `Unregister<TService>()` で外します。

## 5. 仕組み

`MessageHub` の内部は「メッセージ型 → 購読者リスト」の辞書です。発行すると該当型のリストを優先度順
（`Subscribe` の `priority` 引数。小さいほど先、同値は登録順の安定挿入。`SubscribeCommand` は常に0で priority 引数なし）に呼びます。

堅牢性のための作り込み:

- **発行中の変更に安全**: 発行中に `Dispose` された購読は「墓標」になり配達をスキップ、発行中に追加された購読は
  待機列に入り「その発行では呼ばれない」。どちらも発行の入れ子が全て終わった時点でリストに反映されます（挙動を決定的にするため)
- **例外の隔離**: 1購読者が例外を投げても残りへの配達は完了します。例外は集約され、最も外側の発行が終わった後に
  `AggregateException` を包んだ HubException として投げ直されます。ただし観測イベント `DeliveryFailed` に購読者がいれば
  通知のみで飲み込まれます（トレーサを開いている間はこちらの経路）
- **循環検知**: 発行の入れ子深さが `MaxPublishDepth = 32` を超えると「循環発行を検知」の HubException
- **遅延発行**: `PublishDeferred` は struct をキャプチャして配達を予約し、`Pump()` がまとめて配達して件数を返します。
  Pump 中に積まれた遅延発行は次回の Pump へ回ります（無限ループ防止）。命令の処理者不在チェックは
  PublishDeferred 時ではなく **Pump での配達時**に行われます
- **メインスレッド専用**: ロックを持たないため、開発ビルド（DEBUG / UNITY_EDITOR / DEVELOPMENT_BUILD）では
  発行・購読時にスレッドを検査して HubException。リリースでは検査なしで静かに壊れるので開発中に必ず直すこと
- **観測フック**: `MessagePublished`（型・購読者数）と `DeliveryFailed`（型・例外）の2イベント。null なら一切コストなし。
  Hub の発見は `MessageHubRegistry`（`#if UNITY_EDITOR || DEVELOPMENT_BUILD` で丸ごと消える弱参照の台帳。
  ゲームコードから参照禁止）経由で、トレーサはこれを毎秒再走査します

> 📖 **用語 — 弱参照（WeakReference）**: GC がオブジェクトを回収するのを妨げない参照。台帳が
> `WeakReference<MessageHub>` で持つため、使われなくなった Hub は台帳に残っていても回収されます。

> 📖 **用語 — リングバッファ**: 一定件数を超えたら古いものから捨てる記録方式。トレーサの履歴は直近256件
> （`MaxEntries = 256`）のリングバッファです。

三大規約との関係:

- **「状態は Tick、艶は Update」** — MessageHub は Unity メインループ（艶＝見た目・演出の世界）側の連携手段です。
  決定的な状態進行の内側の出来事は GameCore の EventHub が受け持ち、リプレイに記録されます。境界を越えて
  見た目側へ知らせたいときに MessageHub へ「翻訳」します
- **「方針は App」** — メッセージ型は `Seed.Hub.Contracts` で共有しますが、「誰がどの Hub を持ち、誰が処理者になるか」を
  決めるのは App の合成ルートです。`Pump()` をフレーム末尾で呼ぶのも合成ルートの責務です
- **「命令の処理者は1基盤」** — この規約はコメントではなくコードで守らせています。`SubscribeCommand` の二重登録・
  `PublishCommand` の処理者不在・通常 `Subscribe`/`Publish` への命令型の持ち込みは、いずれもその場で HubException です

> 📖 **用語 — LateUpdate**: Unity が全コンポーネントの Update を終えた後に呼ぶイベント関数。
> `Sample_GameFlowRunner` は LateUpdate で `_hub.Pump()` を呼び、入力・フロー・各フェーズの処理が
> 全て終わった後に遅延メッセージを配達することで「今フレームの連鎖の外へ回す」という約束を守っています。

## 6. よくあるつまずき

- **症状**: `PublishDeferred` したメッセージが届かない
  → **原因**: その Hub の `Pump()` を誰も呼んでいない（Pump は自動では走らない）
  → **対処**: 合成ルートのフレーム末尾（LateUpdate）で毎フレーム `Pump()` を呼ぶ。`Sample_GameFlowRunner` は
  LateUpdate で呼んでいるので、自前ルートを作る場合も同じ形にする
- **症状**: HubException「〜は命令（ICommandMessage)。SubscribeCommand を使う」
  → **原因**: 命令型を通常の `Subscribe<T>` / `Publish` に渡した
  → **対処**: 命令は `SubscribeCommand<T>` / `PublishCommand` を使う（処理者1基盤の規約を守らせる入口）
- **症状**: HubException「〜の処理者が未登録（命令が握り潰される。合成ルートを確認）」
  → **原因**: 処理者の配線漏れ。`PublishDeferred` した命令の場合はこの例外が Pump の配達時に出る
  → **対処**: 合成ルートで処理担当の基盤が `SubscribeCommand` しているか確認する
- **症状**: HubException「〜には既に処理者がいる」
  → **原因**: 同じ命令型に `SubscribeCommand` を二重登録した
  → **対処**: 処理者を1基盤に絞る。既存の処理者を差し替えたいなら先に購読を Dispose する
- **症状**: 発行しているのに購読者に何も届かない（例外も出ない）
  → **原因**: 発行側と購読側で別の `MessageHub` インスタンスを使っている（Hub はシングルトンではなく、
  new するたび別の掲示板になる）
  → **対処**: 合成ルートが生成した1個を配る。トレーサの Hub0 / Hub1 表示で複数 Hub の存在に気づける
- **症状**: 発行中に `Subscribe` した購読者が、その発行では呼ばれない
  → **原因**: 仕様（発行中の登録は待機列に入る）
  → **対処**: 次の発行からは呼ばれる。今回の配達にどうしても含めたいなら発行前に購読しておく
- **症状**: トレーサを開いていると購読者の例外が Console に出ない
  → **原因**: トレーサが `DeliveryFailed` を購読するため、例外は投げ直されず観測のみになる（仕様）
  → **対処**: トレーサの履歴で `✕` 付き太字の行を確認する。トレーサを閉じれば集約 HubException が投げられる
- **症状**: Play 直後の最初の発行がトレーサに出ない
  → **原因**: Hub の再走査は毎秒のため、生成直後の Hub の発行を最大1秒分取り逃がす
  → **対処**: 仕様として了解しておく。取り逃がしが問題なら発行側で一時的に Debug.Log を併用する
- **症状**: 別スレッドから Publish すると開発ビルドで HubException
  → **原因**: MessageHub はロックを持たないメインスレッド専用（リリースでは検査なしで購読リストが静かに壊れる）
  → **対処**: 別スレッドの結果はメインスレッドに戻してから発行する

## 7. 増やす・拡張する

- **メッセージ型を増やす**: `Assets/Script/Hub/Contracts/Messages/` に readonly struct を1つ追加し、
  `INotificationMessage` か `ICommandMessage` を実装する。`ChangePhaseCommand.cs` のように
  **命令（`ChangePhaseCommand`）と結果通知（`PhaseChanged`）を同じファイルに同居**させるのが流儀
- **共有IDや問い合わせ窓口を増やす**: `Assets/Script/Hub/Contracts/` に追加する
  （`ScreenId` `PhaseId` `StageId` `CharacterId` `FactionId` `ReactionId` や `ICharacterQuery` `IGameClock` `ISaveStore` の並び）
- **同期問い合わせを増やす**: Contracts にインターフェースを定義 → 提供基盤が実装 → 合成ルートで
  `ServiceRegistry.Register<IF>(impl)`。二重登録は既定で例外、意図的な差し替えは `allowOverwrite: true`
- **依頼-応答を増やす**: `XxxRequested(RequestId, …) : ICommandMessage` と
  `XxxCompleted(RequestId, 成否, …) : INotificationMessage` のペアを定義し、発番は永続ルートが持つ
  `RequestIdSource` の `Next()` で行う（実戦例1参照。1プロセス1個が規約）
- **独自の観測ツールを作る**: `MessageHub.MessagePublished` / `DeliveryFailed` イベントを購読する
  （null なら一切コストなし。開発ビルドでのみ購読する運用でよい）
- **トレーサを調整する**: `MessageHubTracerWindow.cs` の `MaxEntries = 256`（履歴容量）、頻度表示の上位8件、
  履歴行フォーマットを書き換える

## 8. 関連ファイルとテスト

- `Assets/Script/Hub/Runtime/MessageHub.cs` — 本体（通知・命令・遅延発行・観測フック）
- `Assets/Script/Hub/Runtime/SubscriptionBag.cs` — 購読の一括解除と `AddTo` 拡張
- `Assets/Script/Hub/Runtime/ServiceRegistry.cs` — 同期問い合わせの台帳
- `Assets/Script/Hub/Runtime/RequestIdSource.cs` — 相関IDの発番器
- `Assets/Script/Hub/Runtime/MessageHubRegistry.cs` — 開発時専用の Hub 台帳（弱参照）
- `Assets/Script/Hub/Contracts/MessageMarkers.cs` / `RequestId.cs` — 通知・命令の印と相関ID
- `Assets/Script/Hub/Contracts/Messages/ChangePhaseCommand.cs` — 命令＋結果通知の同居例
- `Assets/Script/Hub/Editor/MessageHubTracerWindow.cs` — メッセージトレーサ
- テスト: `Assets/Script/Hub/Tests/Editor/HubTests.cs`（基本動作）・`HubGuaranteeTests.cs`（発行中の変更・例外隔離・
  Pump などの保証）・`RequestResponseTests.cs`（RequestId 相関）

[← 前: 01_Demo](01_Demo.md) | [索引](README.md) | [次: 03_Clock →](03_Clock.md)
