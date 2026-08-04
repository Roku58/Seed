# 03. GameCore — 決定的ゲームロジックと記録/リプレイ

「同じシード＋同じ入力列 → 必ず同じ結果」を規約とする純C#ロジック基盤。
サンプル世界はモンハン風の Hunter/Monster 1v1（ActionBattle）。

## サンプル3種の起動（いずれも空 GameObject に Add Component して Play）

| コンポーネント | 内容 | 操作 |
|---|---|---|
| `Sample_ActionBattleRunner` | 台本デモ→リプレイ検証まで全自動。Console に実況 | なし（Inspector: Logic Seed / Enable Core Trace） |
| `Sample_ActionBattleInteractiveRunner` | 手で遊ぶ。全操作がジャーナルに記録される | [1]斬り上げ→頭 / [2]→翼 / [3]溜め斬り / [4]鬼人薬 / [G]ガード / **[V]リプレイ検証** / [R]決着後リスタート |
| `Sample_ActionBattle3DRunner` | 3D物理当たり判定の統合デモ | WASD移動 / [1][3]攻撃 / [4]鬼人薬 / [G]ガード / [R] |

- Console に「リプレイ検証 OK: hash=XXXXXXXX」が出れば決定性が成立している証拠
  （NG なら決定性が壊れている——ロジックに UnityEngine.Random 等を混ぜていないか疑う）
- 対話型/3D は**旧 Input クラス**使用のため Active Input Handling が「Both」であること（設定済み）

## 決定性の三大規約

1. **記録してから実行**: `journal.Record(now, in input)` → `Execute(in input)` の順
   （App 層では `LogicInputFunnel.Submit` がこの順を強制。クラッシュしても記録が残る）
2. **時間前進も入力**: `AdvanceTime` も記録する（漏らすと再生時に時刻がずれる）
3. **ロジック乱数は ctx.LogicRandom だけ**。演出の抽選は `Fork()` した子ストリームで

## 最小コード（記録→リプレイ→ハッシュ照合）

```csharp
using Seed.Core;
using Seed.Core.Samples.ActionBattle;

const uint seed = 1041u;
var world = new Sample_ActionWorld(seed, trace: null);
var journal = new InputJournal<Sample_ActionInput>(32) { Seed = seed, Version = 1 };
world.Ctx.AddExtension(journal);

// ★記録してから実行
var input = Sample_ActionInput.HunterAttack(
    world.Registry.GetId(world.SlashUp), world.Registry.GetId(world.Head));
journal.Record(world.Ctx.NowMs, in input);
world.Driver.Execute(in input);

// リプレイ: 同じシードで作り直して入力列を流すだけ
var replay = new Sample_ActionWorld(journal.Seed, trace: null);
for (var i = 0; i < journal.Count; i++)
{
    replay.Driver.Execute(journal[i].Input);
}
bool match = Sample_ActionBattleDemo.HashRecords(world.Ctx)
          == Sample_ActionBattleDemo.HashRecords(replay.Ctx);
```

byte[] 保存は `InputJournalCodec.ToBytes/FromBytes`（FNV-1a ハッシュ付き封筒。
破損・版数不一致は null を返して再生拒否）。

## ハマりどころ

- リプレイ検証のハッシュは `IHashableRecord.AddTo`（StableHash）由来。
  `string.GetHashCode()` は実行ごとに変わるため使用禁止
- `DeterministicRandom.Roll` は 0‰以下/1000‰以上で乱数を**消費しない**。
  常に消費する `RollAlwaysConsume` と仕様ごとに使い分けを固定する
- シード 0 は内部で 2463534242u に置換される
- `EntityRegistry.RegisterWithId` の明示 ID を変えると保存済みリプレイが壊れる

## 増やすとき

- 入力種別: `Sample_ActionInputKind` に列挙 → `Sample_ActionInput` にファクトリ →
  `Sample_ActionDriver.Execute` の分岐 → `Sample_ActionInputCodec` に固定順で追加
  （形式が変わったら `InputJournal.Version` を上げる）
- 検証対象レコード: `IHashableRecord` 実装（全フィールドを固定順で Add）
- 巻き戻し: `LogicContext.CaptureAll/RestoreAll`

主要ファイル: `Assets/Script/GameCore/Runtime/Replay/InputJournal.cs` /
`Assets/Script/GameCore/Samples/ActionBattle/Demo/Sample_ActionBattleDemo.cs` /
統合経路 `Assets/Script/App/CoreHubBridge.cs`。
テスト: `SampleIntegrationTests` / `SnapshotReplayTests` / `FuzzTests`（ランダム120入力×5シードで常に一致）
