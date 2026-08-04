# 08. Seed.Input — 入力（Reader → Snapshot → Router → 意味づけ）

分業構造: **IInputReader**（デバイス依存の読み取り・Read() 1メソッド）→
**InputSnapshot**（1フレームの写し・64bit ビットマスク）→ **InputRouter**（エッジ検出）→
**意味づけはアプリの各フェーズ**（同じ[1]キーがホームでは出撃・戦闘では攻撃）。

## デモで確認する

統合デモのすべてのキー操作がこの経路。`Sample_KeyboardReader`（アセット不要の直読み）の割当:

| 物理キー | ActionId |
|---|---|
| WASD | Move ベクトル |
| [1] / [2] / [3] | Attack / Interact / Submit |
| [4] / [5] | Sample_ActionIds.Slot4(32) / Slot5(33)（アプリ独自帯） |
| [G] / [T] / [B] / [P] / [O] | Guard / Next / Cancel / Previous / Jump |

## 最小コード

```csharp
var input = new InputRouter(new Sample_KeyboardReader());

// 毎フレーム1回だけ（永続ルートの Update）:
input.Tick();

// フェーズ側は読むだけ:
if (input.WasPressedThisFrame(ActionId.Attack)) { /* 押した瞬間 */ }
if (input.IsPressed(ActionId.Guard)) { /* 押している間 */ }
var move = input.Move; // Vector2
```

本実装は `InputSystemReader`（.inputactions 駆動・リバインド可能）:
`new InputSystemReader(inputActionAsset)` を InputRouter に渡すだけ。
既定対応表は Player/Attack・Player/Jump 等（Guard は既定に無いので
`reader.Bind(ActionId.Guard, "Player/Guard")` で明示割当）。IDisposable なので OnDestroy で Dispose。

## ハマりどころ

- **`InputRouter.Tick()` は1フレームに1回だけ**。2回呼ぶと WasPressedThisFrame が立たなくなる
- **画面遷移・ポーズ復帰の直後は `InputRouter.Reset()`**。呼ばないと切替前から押していた
  ボタンが「押した瞬間」として湧く（ポーズ解除と同時に攻撃が出る）
- ActionId 0（None）と 64 以上はボタンビットに載らない（範囲外は常に「押されていない」）
- Keyboard.current が null（Input System 無効）だと無反応になる
- AI・リプレイ・リモート入力も IInputReader 実装1つで同じ経路に乗る
  （テスト用 `FakeInputReader` が実例）

## 増やすとき

- 独自ボタン = `new ActionId(32〜63)` を定数クラスに発番 → Reader で物理キーへ割当
- 新デバイス = IInputReader 実装を InputRouter に渡すだけ

主要ファイル: `Assets/Script/Input/Runtime/InputRouter.cs` / `InputSystemReader.cs`。
テスト: `Assets/Script/Input/Tests/Editor/InputRouterTests.cs`
