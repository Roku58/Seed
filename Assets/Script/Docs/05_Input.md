# 05. Seed.Input — 入力（Reader → Snapshot → Router → 意味づけ）

[← 前: 04_Flow](04_Flow.md) | [索引](README.md) | [次: 06_UI →](06_UI.md)

## この章で分かること

- 入力基盤が **Reader → Snapshot → Router → 意味づけ** の4段に分かれている理由と、それぞれの責務
- `WasPressedThisFrame`（押した瞬間）が「押しっぱなしでは連射されない」仕組み（エッジ検出）
- デモ用 `Sample_KeyboardReader` と本番用 `InputSystemReader` の違い、および乗り換え手順（.inputactions の Bind 含む）
- アプリ独自ボタンの発番規約（32〜63）と、64bit ビットマスクという表現の制約
- 画面遷移直後に `Reset()` を呼ばないと起きる誤検出とその対処

## 前提

- [01_Demo](01_Demo.md) — 統合デモ（`Sample_GameFlowRunner`）の起動方法。本章の動作確認はこのデモを使います
- [02_Hub](02_Hub.md) — 規約「基盤から Hub への発行はゼロ」。InputRouter が Hub に何も発行しない理由がここにあります
- [03_Clock](03_Clock.md) — 永続ルートの Update が各基盤の Tick を1本で駆動する構造。`InputRouter.Tick()` もその1本に乗ります
- [04_Flow](04_Flow.md) — フェーズ（ホーム/戦闘/ショップ）。本章の「意味づけ」の担い手です

この章が初出の主な用語: `IInputReader` / `InputSnapshot` / `ActionId` / `InputRouter` / `InputSystemReader` / エッジ検出 / ビットマスク

## 1. これは何か

Seed.Input は「デバイスから入力を読む」ことと「その入力が何を意味するか」を分離するための基盤です。分業は次の4段です。

1. **Reader**（`IInputReader`）— デバイス依存の読み取り。`Read()` 1メソッドだけ
2. **Snapshot**（`InputSnapshot`）— 1フレームぶんの入力の写し。生の状態のみで意味を持たない値
3. **Router**（`InputRouter`）— 前フレームとの差分から「押した瞬間 / 離した瞬間」を出す（エッジ検出）
4. **意味づけ** — アプリの各フェーズの仕事。同じ [1] キーがホームでは「出撃」、戦闘では「攻撃」になる

> 📖 **用語 — エッジ検出**: 信号処理由来の言葉で、「押されている/いない」という状態そのものではなく、状態が変わった瞬間（立ち上がり・立ち下がり）を検出すること。ゲームでは「押した瞬間だけ攻撃を1回出す」ために必須の処理です。

この分離が無いと何に困るか。実際にこのプロジェクトでは、各デモが「前フレームの押下値をフィールドに覚えて今フレームと比較する」コードを別々に書いており、同じ仕組みを何度も再発明していました。比較を1か所忘れると「押しっぱなしで攻撃が連射される」不具合になり、原因が入力の外側にあるように見えてしまいます。差分の持ち主を `InputRouter` 1つに固定したことで、アプリ側は前フレーム値を一切持たなくてよくなりました。

また、読み取りを `IInputReader` という1メソッドの抽象に切ったことで、テスト（偽実装でスナップショットを差し込む）・リプレイ（記録したスナップショット列を順に返す）・リモート/AI 操作（自動生成した入力を返す）がすべて「読み取り実装の一種」として同じ経路に乗ります。

## 2. 全体像

### 部品表

| 部品 | 種類 | 役割 |
|---|---|---|
| `IInputReader` | interface | `Read()` 1メソッド。デバイス依存部分を閉じ込める |
| `InputSnapshot` | readonly struct | 1フレームの写し（Move / Look / Pressed 64bit） |
| `ActionId` | readonly struct | アクション種別ID（enum ではない値型ID） |
| `InputRouter` | 純C# sealed class | エッジ検出。前後フレーム差分の唯一の持ち主 |
| `InputSystemReader` | sealed class（IDisposable） | .inputactions 駆動の本番用 Reader |
| `Sample_KeyboardReader` | sealed class（Seed.App） | アセット不要のデモ用 Reader |

> 📖 **用語 — readonly struct**: C# の値型のうち、生成後にフィールドを書き換えられないと宣言したもの。`InputSnapshot` はこれで「1フレームの写し」が後から改変されないことを型で保証し、かつヒープ確保なしで毎フレーム運べます。

### データの流れ

```
物理デバイス（キーボード / ゲームパッド / ...）
      │ デバイス依存の読み取り（Unity の入力APIを触るのはここだけ）
      ▼
IInputReader.Read() ──→ InputSnapshot（Move, Look, Pressed の値）
      ▼
InputRouter.Tick() ── 今の写しを「前」へ送り、新しい写しを読む
      │ IsPressed / WasPressedThisFrame / WasReleasedThisFrame / Move / Look
      ▼
アプリの各フェーズ（意味づけ: ホームの[1]=出撃、戦闘の[1]=攻撃）
```

`InputSnapshot` の中身は3つだけです。移動 `Move` と視点 `Look` は連続値なので `Vector2`、ボタンは 64bit のビットマスク `Pressed`（ulong）で持ち、`ActionId` の値をそのままビット位置に対応させます。

> 📖 **用語 — ビットマスク**: 1つの整数の各ビット（2進数の桁）を独立したオン/オフのフラグとして使う技法。64bit 整数なら64個のフラグを、配列も辞書も確保せずに1つの値で運べます。「ボタンn が押されている = ビットn が 1」という対応です。

## 3. 動かして試す

統合デモのすべてのキー操作がこの経路を通ります。

1. **File > New Scene** で空シーンを作成（既存シーンへの配置は不要。ランナーがカメラ・ライトも自動生成します）
2. **GameObject > Create Empty** で空の GameObject を作成
3. Inspector の **Add Component** で `Sample_GameFlowRunner` を追加
4. **Play** を押す

期待される動き: Play 直後にホームのメニューパネル（[1]〜[5] の項目）が表示されます。[1] を押すと戦闘フェーズへ遷移し、WASD でプレイヤーが動きます。[B] でホームへ戻れます。

`Sample_KeyboardReader` の物理割り当て（固定・Look は常に `Vector2.zero`）:

| 物理キー | ActionId | ホームでの意味 | 戦闘での意味 |
|---|---|---|---|
| WASD | Move ベクトル | — | 移動 |
| [1] | Attack (1) | 出撃: ステージ1 | 攻撃 |
| [2] | Interact (4) | 出撃: ステージ2 | — |
| [3] | Submit (6) | ショップへ | — |
| [4] | Sample_ActionIds.Slot4 (32) | 出撃: ステージ3＝迷宮 | — |
| [5] | Sample_ActionIds.Slot5 (33) | 出撃: ステージ4＝市街 | — |
| [G] | Guard (2) | — | ガード（押しっぱなし） |
| [T] | Next (10) | — | 3D⇔2D アバター切替に流用 |
| [B] | Cancel (5) | — | ホームへ戻る（ショップでも同じ） |
| [P] | Previous (9) | — | ポーズに流用 |
| [O] | Jump (3) | — | 自動操縦切替に流用 |

エッジ検出を体感するには、戦闘中に [1] を**押しっぱなし**にしてみてください。攻撃は最初の1回しか出ません（`WasPressedThisFrame` は押した最初のフレームしか true にならない）。一方 [G] のガードは押している間ずっと効きます（`IsPressed` で判定しているため）。同じ Reader・同じ Snapshot でも、フェーズ側がどの問い合わせを使うかで挙動が変わる——これが「意味づけはアプリ」の具体例です。

## 4. コードで使う

### 最小例

```csharp
using Seed.App;   // Sample_KeyboardReader
using Seed.Input; // InputRouter, ActionId

var input = new InputRouter(new Sample_KeyboardReader()); // reader が null なら ArgumentNullException

// 永続ルートの Update で、毎フレームちょうど1回だけ:
input.Tick();

// フェーズ側は読むだけ（前フレーム値を自分で持たない）:
if (input.WasPressedThisFrame(ActionId.Attack)) { /* 押した瞬間だけ true */ }
if (input.IsPressed(ActionId.Guard))            { /* 押している間ずっと true */ }
if (input.WasReleasedThisFrame(ActionId.Guard)) { /* 離した瞬間だけ true（ため攻撃の解放など） */ }
var move = input.Move; // Vector2（今フレームの移動入力）
```

> 📖 **用語 — 永続ルート**: シーンをまたいで生き続ける唯一の MonoBehaviour（デモでは `Sample_GameFlowRunner`）。各基盤の Tick をここから1本で呼ぶのがプロジェクト規約です。デモでは `Update()` が `_clock.Tick(Time.deltaTime)` → `_input.Tick()` → `_flow.Tick(...)` の順に駆動しています。

### 実戦例 — InputSystemReader（.inputactions 駆動）へ乗り換える

`Sample_KeyboardReader` はデモ都合の最小実装（アセット不要・リバインド不可）で、ソースコメントにも「実プロジェクトでは `InputSystemReader` を使うこと」と明記されています。乗り換えはコンストラクタに渡す Reader を差し替えるだけです。

```csharp
using Seed.Input;
using UnityEngine;
using UnityEngine.InputSystem; // InputActionAsset

/// <summary>InputSystemReader を配線した永続ルートの例。</summary>
public sealed class MyGameRunner : MonoBehaviour
{
    [SerializeField] private InputActionAsset _actions; // .inputactions を Inspector で割り当てる

    private InputSystemReader _reader;
    private InputRouter _input;

    private void Start()
    {
        // 既定対応表（DefaultButtonBindings）でアクション名を解決する。
        // 解決はコンストラクタで一度だけ行われ、以後の Read は解決済み参照を読むだけ
        _reader = new InputSystemReader(_actions);

        // Guard は既定アセットに対応アクションが無いため既定対応表に載っていない。
        // アプリが Bind で明示的に割り当てる（既存の割り当ては上書きされる）
        _reader.Bind(ActionId.Guard, "Player/Guard");

        // 解決できたアクションの所属マップ（Player と UI）をまとめて有効化
        _reader.Enable();

        _input = new InputRouter(_reader);
    }

    private void Update()
    {
        _input.Tick(); // 1フレームにちょうど1回
    }

    private void OnDestroy()
    {
        _reader?.Dispose(); // IDisposable。Dispose 後の Read は InputSnapshot.Empty を返す
    }
}
```

> 📖 **用語 — InputActionAsset (.inputactions)**: Unity Input System Package の入力設定アセット。「Player/Move」のようにマップ名＋アクション名でアクションを定義し、物理キーとの対応（バインド）をエディタで編集・リバインドできます。Unity 既定の `InputSystem_Actions.inputactions` には Player マップ（Move/Look/Attack/Interact/Crouch/Jump/Previous/Next/Sprint）と UI マップ（Submit/Cancel ほか）が入っています。

> 📖 **用語 — IDisposable**: 使い終わったら明示的に後始末（Dispose）が必要なことを表す C# の標準インターフェース。`InputSystemReader` は入力マップの無効化と参照の解放のためにこれを実装しており、永続ルートの OnDestroy から Dispose を呼びます（二重 Dispose は無害）。

コンストラクタの完全なシグネチャは次のとおりで、対応表・アクション名はすべて差し替え可能です。

```csharp
public InputSystemReader(
    InputActionAsset asset,                                          // null なら ArgumentNullException
    IEnumerable<KeyValuePair<ActionId, string>> buttonBindings = null, // 省略時 DefaultButtonBindings
    string moveActionName = "Player/Move",                           // DefaultMoveActionName
    string lookActionName = "Player/Look")                           // DefaultLookActionName
```

既定対応表 `DefaultButtonBindings` の中身: Attack=`Player/Attack`, Jump=`Player/Jump`, Interact=`Player/Interact`, Sprint=`Player/Sprint`, Crouch=`Player/Crouch`, Previous=`Player/Previous`, Next=`Player/Next`, Submit=`UI/Submit`, Cancel=`UI/Cancel`。マップ名で修飾するのは、同名アクションが複数マップにある場合の取り違えを防ぐためです。

## 5. 仕組み

### InputRouter — 差分の唯一の持ち主

`Tick()` の中身は2行だけです。今の写しを `_previous` へ送り、Reader から新しい写しを `_current` に読む。エッジ判定はその差分です。

- `WasPressedThisFrame(a)` = 今押されている **かつ** 前フレームは押されていない
- `WasReleasedThisFrame(a)` = 今押されていない **かつ** 前フレームは押されていた
- `Reset()` = 今を読み直して「前＝今」に揃える（差分の基準を今に置き直す）

`Current` / `Previous` プロパティで写しそのものも読めるため、独自の差分判定やデバッグ表示（`ToString()` はマスクを16進表示）にも使えます。

### InputSnapshot — 確保ゼロで運ぶ

ボタンは可変個ですが、readonly struct に配列や辞書を持たせると毎フレームの確保とヌル分岐が増えます。そこで押下状態は 64bit ビットマスクで表し、`MaskOf(action)` が `ActionId.Value` 1〜63 に対して `1UL << value` を、範囲外（0以下・64以上）に対して 0 を返します。範囲外は例外ではなく常に「押されていない」——入力1つの配線ミスでゲームが落ちるより、警告で気付かせる方が実用的という判断です。マスクの組み立ては `InputSnapshot.SetPressed(pressed, action, isPressed)` に1か所だけ置かれ、Reader 実装とテストダブルの双方がビット演算を書き写さずに済みます。

### ActionId — enum にしない理由

アクションは「ゲームごとに増える語彙」なので、アプリ側が基盤に手を入れず追加できる値型IDにしています（`ScreenId` / `BehaviorKey` と同じ流儀）。割り当て規約は **0 = None（未指定・ボタン不可）/ 1〜99 = 基盤予約 / 100以降 = アプリ独自**。ただしビットマスクに載るのは 1〜63 だけなので、**アプリ独自の「ボタン」は空き番の 32〜63 を使い**、100以降は軸・ジェスチャ等ボタン以外の将来用番号帯とします。この非対称さは「確保ゼロの readonly struct で運ぶ」ことの対価です。基盤予約済みは 1〜10（Attack/Guard/Jump/Interact/Cancel/Submit/Sprint/Crouch/Previous/Next）。

### InputSystemReader — 解決は一度・失敗は警告

アクション名の解決（`InputActionAsset.FindAction`）はコンストラクタと `Bind` で一度だけ行い、毎フレームの `Read()` は解決済みの `InputAction` 参照を読むだけです（Move/Look は `ReadValue<Vector2>()`、ボタンは `InputAction.IsPressed()`）。名前が見つからない場合は例外にせず、同じ名前につき1回だけ警告して「常に無反応」として扱います。有効化は解決できたアクションが属する `InputActionMap` 単位で行うため、既定アセットのように Player と UI へアクションが散っている構成をそのまま扱えます。

### 三大規約との関係

- **状態は Tick、艶は Update** — 入力状態の更新は `InputRouter.Tick()` に集約され、永続ルートの Update が1フレームに1回だけ呼びます。実行順を Script Execution Order に頼らないため、InputRouter は MonoBehaviour ではなく純C# クラスです
- **方針は App** — Reader も Router も「[1] が押された」という事実しか扱いません。それが出撃か攻撃かはフェーズ（`Sample_HomePhase` / `Sample_BattlePhase` 等）の Tick 内の分岐が決めます
- **命令の処理者は1基盤** — エッジ検出の処理者は InputRouter ただ1つ。また InputRouter は Hub へ何も発行しません（基盤から Hub への発行はゼロ）。「Attack が押された → AttackRequested を発行」といった翻訳もアプリの方針の仕事です

## 6. よくあるつまずき

- **症状**: `WasPressedThisFrame` が一度も true にならない → **原因**: `Tick()` を1フレームに2回呼んでいる（前＝今になり差分が消える） → **対処**: 呼び元を永続ルートの Update 1か所に固定する。デモでは `Sample_GameFlowRunner.Update` が唯一の呼び元
- **症状**: ポーズ解除・画面遷移の直後、押しっぱなしだったボタンが「押した瞬間」として発火する（ポーズ解除と同時に攻撃が出る） → **原因**: 差分の基準が切替前のまま → **対処**: 切替直後に `InputRouter.Reset()` を呼ぶ
- **症状**: 独自ボタンが常に無反応 → **原因**: `ActionId` が範囲外（0 や 64 以上はビットに載らない） → **対処**: 独自ボタンは 32〜63 で発番。`InputSystemReader.Bind` に範囲外IDを渡した場合は警告「`[Seed.Input] Action#N はボタンとして扱えません（1〜63 のIDのみ）...`」が出ます
- **症状**: `InputSystemReader` で特定のアクションだけ無反応 → **原因**: アクション名の綴り違い、または `Enable()` の呼び忘れ → **対処**: Console に1回だけ出る警告「`[Seed.Input] アクション '名前' が アセット名 に見つかりません。この入力は常に無反応として扱います。`」を確認（毎フレームは出ません）。警告が無いのに無反応なら `Enable()` を確認
- **症状**: キーボードが全く効かない → **原因**: `Keyboard.current` が null（Input System が無効な環境） → **対処**: Sample_KeyboardReader は null 時に `InputSnapshot.Empty` を返す仕様。Package Manager で Input System の導入状態を確認
- **症状**: `UnityEngine.Input` を使ったコードが実行時例外になる → **原因**: このプロジェクトの activeInputHandler は「Input System Package 専用」 → **対処**: 旧 Input は直叩きせず、読み取りは `IInputReader` 実装（実質 `InputSystemReader`）に集約する
- **症状**: Dispose 後も Read され続けて不安 → **対処不要**: Dispose 後の `Read()` は `InputSnapshot.Empty` を返す安全設計（二重 Dispose も無害）

## 7. 増やす・拡張する

### アプリ独自ボタンを増やす

1. `Assets/Script/App/Samples/Sample_ActionIds.cs` のような定数クラスに `public static readonly ActionId MyButton = new ActionId(34);` を発番（32〜63 の空き番から）
2. Reader 側で割り当てる。`Sample_KeyboardReader` なら `Read()` に `pressed = InputSnapshot.SetPressed(pressed, MyActionIds.MyButton, keyboard.qKey.isPressed);` を1行追加。`InputSystemReader` なら `reader.Bind(MyActionIds.MyButton, "Player/MyAction")`
3. フェーズ側で `WasPressedThisFrame(MyActionIds.MyButton)` を読んで意味づけする

### .inputactions に Guard を追加して Bind する

1. Project ビューで `.inputactions` アセットをダブルクリックして Input Actions エディタを開く（新規に作るなら **Assets > Create > Input Actions**）
2. 左列で **Player** マップを選び、Actions 列の **+** で `Guard` アクションを追加（Action Type は Button）
3. `Guard` の下のバインドに物理キー（例: `<Keyboard>/g`）を割り当て、**Save Asset** を押す
4. コード側で `reader.Bind(ActionId.Guard, "Player/Guard")` を呼ぶ（4節の実戦例のとおり）

### 新しいデバイス・リプレイ・AI 操作を足す

`IInputReader` を実装して `InputRouter` のコンストラクタに渡すだけです。実装は「読むだけ」に徹し、意味づけをしないこと。テスト用の `FakeInputReader`（`SetMove` / `SetPressed` / `SetSnapshot` で任意の写しを差し込める）が最小の実例で、実デバイスも実行ループも無しに InputRouter を検証しています。

> 📖 **用語 — テストダブル**: テストで本物の代わりに使う偽実装の総称。`IInputReader` が1メソッドなので、テストダブルはフィールド1つと `Read()` だけで書けます。抽象を薄く保つ設計の実利がここに出ます。

### キーの意味づけを変える

Reader には触りません。各フェーズ（`Sample_HomePhase` / `Sample_BattlePhase` / `Sample_ShopPhase`）の Tick 内の `WasPressedThisFrame` 分岐を書き換えます。

## 8. 関連ファイルとテスト

- `Assets/Script/Input/Runtime/IInputReader.cs` — 読み取りの抽象（1メソッド）
- `Assets/Script/Input/Runtime/InputSnapshot.cs` — 1フレームの写し（ビットマスク・`MaskOf` / `SetPressed`）
- `Assets/Script/Input/Runtime/ActionId.cs` — アクションID（発番規約のdocコメントあり）
- `Assets/Script/Input/Runtime/InputRouter.cs` — エッジ検出（`Tick` / `Reset` / `WasPressedThisFrame`）
- `Assets/Script/Input/Runtime/InputSystemReader.cs` — 本番用 Reader（`Bind` / `Enable` / `Dispose`）
- `Assets/Script/App/Samples/Sample_KeyboardReader.cs` — デモ用 Reader（アセット不要）
- `Assets/Script/App/Samples/Sample_ActionIds.cs` — 独自発番の実例（Slot4=32, Slot5=33）
- `Assets/Script/App/Samples/Sample_GameFlowRunner.cs` — 永続ルート（`InputRouter` の配線と Tick の唯一の呼び元）
- テスト: `Assets/Script/Input/Tests/Editor/InputRouterTests.cs`（EditMode 9件）＋ `FakeInputReader.cs` — Window > General > Test Runner の EditMode で実行

[← 前: 04_Flow](04_Flow.md) | [索引](README.md) | [次: 06_UI →](06_UI.md)
