# 16. Seed.World — 原点回帰（広大なフィールドでの座標精度の維持）

[← 前: 15_Pooling](15_Pooling.md) | [索引](00_Roadmap.md) | [次: 17_Tests →](17_Tests.md)

## この章で分かること

- 原点から遠ざかると表示が震え・判定が不安定になる理由（float の精度）
- 「世界全体を原点へ引き戻す」原点回帰の仕組みと、追従の3経路
- 座標を持っている各主体（Transform・純C#座標・Cinemachine）の追従方法
- 決定性（リプレイ）と原点回帰の関係

## 前提

- [02_Hub.md](02_Hub.md) — 通知の購読
- [14_Cameras.md](14_Cameras.md) — カメラの追従（原点回帰と連動する）

この章が初出の主な用語: 浮動小数点の有効桁 / Floating Origin / 格子への丸め。

## 1. これは何か

> 📖 **用語 — float の有効桁**: 単精度浮動小数は「約7桁」の精度しかなく、**絶対値が
> 大きいほど刻みが粗くなる**。座標 10,000m 地点では約 1mm、100,000m では約 1cm 刻みでしか
> 位置を表せない。静止しているのに表示が震える・IK や当たり判定がガタつくのはこのため。

広いフィールドを歩き続けるゲームでは、プレイヤーが原点から数 km 離れた時点で上記の
症状が出ます。対策は座標の型を変えることではなく、**「プレイヤーが遠くへ行ったら、
世界全体を原点方向へ平行移動する」**（Floating Origin）——見た目は何も変わらず、
座標の絶対値だけが小さくなって精度が回復します。

## 2. 全体像

| 部品 | 役割 |
|---|---|
| `OriginShifter`（純C#） | 判断役。閾値・格子への丸め・累積量・絶対座標との相互変換 |
| `OriginShiftSystem` | 実行役。根 Transform の一括移動・追従契約への通知・Hub 通知 |
| `IOriginShiftHandler` | 純C#側に座標を持つものの追従契約 |
| `OriginShifted`（通知） | `Hub/Contracts/Messages/OriginShifted.cs`。Delta と累積 TotalOffset を運ぶ |

### 追従の3経路（座標を持っている主体の一覧）

| 主体 | 追従方法 |
|---|---|
| ステージ施工物・カメラリグ・プール（Transform を持つもの） | **根だけ**を `AddRoot` 登録 → 一括移動（子は勝手に付いてくる） |
| ActorPose・トリガー位置・経路点（純C#側の座標） | `IOriginShiftHandler` を実装して自分で Delta を加算 |
| 他の基盤（疎結合に知りたいもの） | Hub の `OriginShifted` 通知を購読 |
| Cinemachine（追従の履歴を内部に持つ） | `CameraDirector` が `OnTargetObjectWarped` で補正（14章） |
| 揺れもの（SpringBone） | 根本の瞬間移動を自動検出してリセット（09章の仕組みがそのまま効く） |

## 3. 動かして試す

デモは閾値を**45m**（歩いて届く距離）に下げてあります。

1. 統合デモで [1] 草原へ出撃し、WASD で一方向へ走り続ける
2. 原点から 45m を超えた瞬間、Console に
   `[OriginShift] 世界を (…) ずらした（累積 (…)）` が出る
3. **画面上は何も起きない**のが正解——ステージ・キャラ・カメラ・エフェクトが全部
   同じ量だけ動くので、相対関係は変わらない
4. Hierarchy でプレイヤーの位置を見ると、走り続けているのに座標が原点付近へ戻っている

## 4. コードで使う

### 最小例 — 合成ルートで組む（`Sample_BattlePhase.BuildOriginShift` の骨格）

```csharp
var system = host.AddComponent<OriginShiftSystem>();
system.Initialize(_hub, focus: playerTransform, threshold: 2000f); // 実ゲームは km 単位
system.AddRoot(_stageRoot.transform);        // 最上位の根だけを登録（子は付いてくる）
system.AddHandler(new MyOriginFollower(this)); // 純C#座標の追従契約

// 毎フレーム（TickPipeline の Simulation で）
system.Tick(); // 閾値超過を検知したフレームだけずらす
```

### 実戦例 — 純C#側の座標を追従させる

```csharp
/// <summary>純C#座標（演出座標・トリガー位置）の追従。</summary>
private sealed class MyOriginFollower : IOriginShiftHandler
{
    public void OnOriginShifted(Vector3 delta)
    {
        pose.Position += delta;                 // ActorPose（演出座標）
        for (...) triggers[i].Position += delta; // トリガー位置
        footIk.ResetFollow();                    // 時間追従系はワープ扱いで捨てる
    }
}
```

### 絶対座標が要るとき（セーブ・ログ・遠距離判定）

```csharp
var absolute = system.Shifter.ToAbsolute(shiftedPosition); // 見た目 → 開始時の座標系
var shifted = system.Shifter.ToShifted(savedPosition);     // 復元時はその逆
```

## 5. 仕組み

- **判断（OriginShifter・純C#）と適用（OriginShiftSystem）を分離**——閾値・丸め・累積・
  往復変換は EditMode テスト8件で数値検証できる
- 適用順は「①根の一括移動 → ②追従契約 → ③Hub 通知」。①で大半が片付き、②③は
  純C#側の座標だけが対象
- **格子への丸め（SnapSize）**: ずらし量をタイルサイズの倍数へ丸めると、タイル地形や
  ノイズ模様が同じ位相で並び続け、シフトの瞬間に模様が飛ばない
- **決定性との関係**: 原点回帰は「見た目の座標系の付け替え」であってゲームの内容ではない。
  リプレイと組み合わせる場合は、記録を絶対座標（`ToAbsolute`）で行うか、シフト自体を
  入力としてジャーナルに載せる（12章の「時間前進も入力」と同じ考え方）
- 原点は世界の状態なので判断は Tick で行う（「状態は Tick、艶は Update」）

## 6. よくあるつまずき

- **症状**: シフトの瞬間に一部だけ取り残される → **原因**: その主体が3経路のどれにも
  乗っていない（新しい基盤が独自に座標を持った） → **対処**: 2節の表に当てはめて
  `AddRoot` / `AddHandler` / 通知購読のどれかを配線
- **症状**: シフトの瞬間にカメラが吹っ飛ぶ → **原因**: Cinemachine への `OnTargetObjectWarped`
  補正が無い → **対処**: `CameraDirector.Initialize` 済みなら自動（14章）
- **症状**: 二重にずれる → **原因**: 親子関係にある Transform を両方 `AddRoot` した →
  **対処**: 登録は「最上位の根」だけ
- **症状**: セーブした位置がロード後にずれる → **原因**: 見た目の座標のまま保存した →
  **対処**: 保存は `ToAbsolute`、復元は `ToShifted` を通す

## 7. 増やす・拡張する

- 閾値・丸め・高さ方向の有無 = `Initialize` の引数（実ゲームは threshold 1000〜4000m が目安）
- テレポート直後に整えたい = `ShiftNow(delta)`（累積にも記録され、変換は保たれる）
- ミニマップ・座標表示 = `OriginShifted` 通知の `TotalOffset` を購読して補正

## 8. 関連ファイルとテスト

- `Assets/Script/World/Runtime/`（OriginShifter / OriginShiftSystem）
- デモ統合: `Sample_BattlePhase.cs` の `BuildOriginShift` / `ShiftOwnCoordinates`
- テスト: `Assets/Script/World/Tests/Editor/OriginShiftTests.cs`（8件——閾値・丸め・累積・往復・純度）

[← 前: 15_Pooling](15_Pooling.md) | [索引](00_Roadmap.md) | [次: 17_Tests →](17_Tests.md)
