# 16. Seed.World — 原点回帰（広大なフィールドでの座標精度）

[← 前: 15_Pooling](15_Pooling.md) | [索引](README.md) | [次: 17_Tests →](17_Tests.md)

## この章で分かること

- 原点から遠ざかると何が壊れるのか（float の精度と、その見え方）
- 世界全体を原点へ引き戻す仕組みと、誰が追従する必要があるか
- 座標を持っている主体の一覧と、それぞれの追従方法
- 決定性（リプレイ）と原点移動の関係
- 統合デモで 45m 歩いたときに何が起きるか

## 前提

- [02_Hub.md](02_Hub.md) — 原点移動は通知（`OriginShifted`）で全基盤へ伝わります
- [08_Character.md](08_Character.md) — 演出座標（`ActorPose`）は純C#側にあるため、自分で追従させる必要があります
- [14_Cameras.md](14_Cameras.md) — Cinemachine は対象の過去位置を覚えているので専用の通知が必要です

この章が初出の主な用語: 原点回帰（Floating Origin）/ 単精度浮動小数（float）の精度 / 累積オフセット / 格子への丸め（SnapSize）/ 追従契約（`IOriginShiftHandler`）/ 焦点（focus）。

## 1. これは何か

`Seed.World` は「プレイヤーが原点から離れすぎたら、世界全体を原点へ引き戻す」基盤です。
見た目は何も変わらないまま、座標の精度だけが回復します。

> 📖 **用語 — 単精度浮動小数（float）の精度**: float は約7桁の有効数字しか持たないため、
> **絶対値が大きくなるほど表現できる刻みが粗くなります**。原点付近では 0.0000001m 単位で
> 位置を表せますが、原点から 10,000m 離れると刻みは約 0.001m、100,000m では約 0.01m になります。
> 刻みが粗いと「静止しているのに位置が震える」「当たり判定が不安定になる」といった症状が出ます。

無いと困ることは、遠くへ行くほど症状が重くなる形で現れます。

1. **表示が震える**（位置ジッター）。カメラと対象の両方が粗い刻みに丸められ、
   相対位置が毎フレームわずかに変わるため、静止していても輪郭が揺れます
2. **当たり判定・IK が不安定になる**。レイキャストの起点や足の接地点が刻みに丸められ、
   接地判定が交互に入れ替わる（足が地面にめり込む/浮くを繰り返す）
3. **物理が破綻する**。速度・衝突の計算誤差が積み上がり、貫通やすり抜けが起きやすくなります

座標系を倍精度にする（`double` にする）解決もありますが、Unity の `Transform` は float なので
エンジンごと置き換えることになります。**世界をずらす**方が現実的で、
広いフィールドを扱うゲームでは定番の手法です。

> 📖 **用語 — 原点回帰（Floating Origin）**: プレイヤーが一定距離離れたら、
> プレイヤーと世界の全オブジェクトを同じ量だけ平行移動して、プレイヤーを原点付近へ戻す手法。
> 相対関係は変わらないので、プレイヤーからは何も起きていないように見えます。

## 2. 全体像

### 部品表

| 部品 | 場所 | 役割 |
|---|---|---|
| `OriginShifter` | `Assets/Script/World/Runtime/OriginShifter.cs` | 純C#。いつ・どれだけずらすかの判断と累積量の記録 |
| `OriginShiftSystem` | `Assets/Script/World/Runtime/OriginShiftSystem.cs` | MonoBehaviour。適用（Transform の移動）と告知 |
| `IOriginShiftHandler` | 同上 | 追従契約（純C#側に座標を持つものが実装する） |
| `OriginShifted` | `Assets/Script/Hub/Contracts/Messages/OriginShifted.cs` | 通知（Delta と累積 TotalOffset） |

判断（`OriginShifter`）と適用（`OriginShiftSystem`）を分けているのは、
閾値・丸め・累積・座標変換という**判断部分だけを EditMode で検証できる**ようにするためです。

### 適用の順序

```
OriginShiftSystem.Tick()
   │ 焦点（通常はプレイヤー）の位置を見る
   ├─ OriginShifter.TryShift(focus, out delta) … 閾値を超えていて、丸めても 0 でなければ確定
   │
   ├─ ① 登録された根 Transform をまとめて移動（地形・カメラリグ・プールの親…）
   ├─ ② IOriginShiftHandler へ通知（純C#側に座標を持つものが自分で加算）
   └─ ③ Hub へ OriginShifted 通知（疎結合に追従したいもの。Cinemachine への補正もここ）
```

> 📖 **用語 — 焦点（focus）**: 原点回帰の基準にする対象。通常はプレイヤーの表示物です。
> この対象が原点から離れた距離で判断するので、マルチプレイのように基準が複数ある場合は
> 「全員の重心」や「ホストのプレイヤー」など、ゲーム側で1つに決める必要があります。

### 座標を持っている主体と追従方法

| 主体 | 追従方法 |
|---|---|
| ステージの施工物・エフェクト・カメラリグ | **根 Transform を1つ登録**（`AddRoot`）。子は一緒に動く |
| 演出座標（`ActorPose`） | 純C#側にあるので**自分で加算**（追従契約 or 通知） |
| トリガー位置・経路点・追跡目標 | 同上（純C#の座標） |
| Cinemachine のカメラ | `CinemachineCore.OnTargetObjectWarped` を呼ぶ（`CameraDirector` が通知購読で実施） |
| 足IK の時間追従 | 追従状態を捨てる（`FootIkRig.ResetFollow`）。前フレームとの差で動くため |
| 揺れもの（`SpringBoneRig`） | テレポート検出が自動で働く（既定 2m 超の瞬間移動でリセット） |
| Rigidbody・CharacterController | 親の移動に追従する（Transform ベースのため） |

> 📖 **用語 — 根 Transform を登録する意味**: 親を動かせば子は全部動くので、
> 「最上位のものだけ」を登録します。親子関係があるものを両方登録すると
> **二重にずれる**ので、フェーズのルートのような最上位だけを渡します。

> 📖 **用語 — 位置ジッター**: 座標の刻みが粗くなることで、静止しているのに表示位置が
> 毎フレームわずかに変わる現象。輪郭が震えて見えます。原点回帰が防ぐ主症状のひとつです。

## 3. 動かして試す

統合デモは閾値を **45m** にしてあります（実際のゲームでは 1000m 単位ですが、
歩いて到達できる距離にして観察しやすくしています）。

1. 空の GameObject に `Sample_GameFlowRunner` を付けて **Play** → **[1]** で草原へ出撃
2. **W キーを押し続けて**まっすぐ歩く（敵から離れる方向でよい）
3. 原点から 45m を超えた瞬間、Console に次のログが出ます

```
[OriginShift] 世界を (-3.2, 0.0, -45.1) ずらした（累積 (-3.2, 0.0, -45.1)）
```

期待される結果:

- **画面上は何も起きません**（プレイヤーと世界が同じだけ動くので相対関係は不変）
- Hierarchy で `Stage_Grassland` の Transform を見ると、Position が 0 から大きな値へ変わっています
- カメラは飛びません（`CameraDirector` が Cinemachine へ補正を通知しているため）
- 足IK も一瞬乱れません（追従状態を捨てているため）
- さらに歩くと2回目・3回目のログが出て、累積量が増えていきます

比較のために追従を止めてみると理解が早いです。`Sample_BattlePhase.ShiftOwnCoordinates` の
`ShiftAgentPose` の呼び出しをコメントアウトすると、**キャラだけが元の位置に取り残されて
地形の外へ飛び出します**（純C#側の座標は自分で追従させる必要がある、の実演）。

## 4. コードで使う

### 最小例 — 自動判断で世界をずらす

```csharp
using Seed.World;
using UnityEngine;

// 焦点（通常はプレイヤーの表示物）を渡して初期化する
var host = new GameObject("OriginShift");
var system = host.AddComponent<OriginShiftSystem>();
system.Initialize(hub, playerTransform, threshold: 2000f, snapSize: 500f);

// 世界の最上位をまとめて登録（子は一緒に動く）
system.AddRoot(stageRoot);
system.AddRoot(effectRoot);

// 毎フレーム判断する（状態なので Tick で回す）
system.Tick();

// 絶対座標との変換（セーブ・ログ・遠距離判定に使う）
var absolute = system.Shifter.ToAbsolute(transform.position);
var shifted = system.Shifter.ToShifted(savedAbsolutePosition);
```

### 実戦例 — 純C#側の座標を追従させる（デモの構成）

`Transform` を持たない座標は自分で加算します。方針はゲーム側にあるので、
App が追従契約を実装します。

```csharp
/// <summary>
/// 原点回帰への追従（App の方針実装）。
/// Transform を持つものは根をずらせば済むが、純C#側に持っている座標——
/// 演出座標（ActorPose）とトリガー位置——はここで自分で追従させる。
/// </summary>
private sealed class Sample_OriginFollower : IOriginShiftHandler
{
    private readonly Sample_BattlePhase _phase;

    public Sample_OriginFollower(Sample_BattlePhase phase) { _phase = phase; }

    /// <summary>世界がずれたので自前の座標も合わせる。</summary>
    public void OnOriginShifted(Vector3 delta) { _phase.ShiftOwnCoordinates(delta); }
}

/// <summary>純C#側に持っている座標を原点移動へ追従させる。</summary>
private void ShiftOwnCoordinates(Vector3 delta)
{
    ShiftAgentPose(_playerId, delta);
    ShiftAgentPose(_enemyId, delta);
    for (var i = 0; i < _triggers.Count; i++)
    {
        _triggers[i].Position += delta;
    }
    // 足IKの時間追従は「前フレームからの差」で動くので、ワープ相当の移動では捨てる
    _playerFeet?.ResetFollow();
}
```

登録は組み立て時に1行です。

```csharp
_originShift.Initialize(_hub, focus, threshold: 45f);
_originShift.AddRoot(_stageRoot.transform);          // 地形・カメラ・プールごと動かす
_originShift.AddHandler(new Sample_OriginFollower(this));
```

### 実戦例 — Hub 通知で疎結合に追従する

追従契約を登録できない（相手を知らない）場合は通知を購読します。
`CameraDirector` がこの形です。

```csharp
_hub.Subscribe<OriginShifted>(message =>
{
    var delta = new Vector3(message.Delta.X, message.Delta.Y, message.Delta.Z);
    CinemachineCore.OnTargetObjectWarped(_trackingTarget, delta);
}).AddTo(_bag);
```

## 5. 仕組み

### 判断の3段（閾値 → 丸め → 確定）

```csharp
bool TryShift(Vector3 focusPosition, out Vector3 delta)
```

1. **閾値**: 焦点の距離が `Threshold` 以上か。既定は水平距離のみを見ます
   （`ShiftVertical` を true にすると高さも見ます。飛行・宇宙ゲーム向け）
2. **丸め**: `SnapSize` が正なら、ずらす量をその倍数へ丸めます
3. **確定**: 丸めた結果 0 になったらずらしません（無駄なシフトを避ける）。
   確定すると `TotalOffset` に累積し、`ShiftCount` が増えます

> 📖 **用語 — 格子への丸め（SnapSize）**: ずらす量を例えば 500m の倍数に揃えること。
> タイル状の地形やノイズ由来の模様は座標に対して周期的なので、格子に揃えておくと
> **シフトの瞬間に模様の位相が飛びません**。丸めると焦点は完全に原点へは戻りませんが、
> 精度回復の目的には十分です。

> 📖 **用語 — 累積オフセット（TotalOffset）**: これまでにずらした量の合計。
> 「今の見た目の座標」と「ゲーム開始時の座標系での位置」を行き来するための変換値です。
> セーブやクエストマーカーのように「世界の中の絶対位置」が必要な場面で使います。

### 累積オフセットと絶対座標

`TotalOffset` は「開始時の座標系から、今の見た目の座標系までのずれ」です。

```
見た目の座標 − TotalOffset ＝ 開始時からの絶対座標
```

セーブ・ログ・遠距離のクエストマーカーなど「世界の中の絶対的な位置」が必要な場面では
`ToAbsolute` / `ToShifted` で変換します。**セーブは絶対座標で行う**のが安全です
（ロード時の累積量は 0 なので、見た目の座標をそのまま保存すると位置がずれます）。

> 📖 **用語 — テレポート検出**: 「1フレームで大きく動いた」ことを検知して内部状態を捨てる仕組み。
> 揺れもの（`SpringBoneRig`）は既定 2m 超の瞬間移動で自動リセットするため、
> 原点移動でも髪が鞭のように吹き飛びません（→ [09_Motion.md](09_Motion.md)）。

### 決定性（リプレイ）との関係

原点移動は「見た目の座標系の付け替え」であり、ゲームの内容ではありません。
ただし `ActorPose` のような座標を書き換えるため、**リプレイに載せるなら扱いを決める必要があります**。

- **記録を絶対座標で行う**（`ToAbsolute` を通す）——推奨。原点移動が記録に影響しません
- **移動量も入力として記録する**——原点移動のタイミングまで再現したい場合

デモの GameCore ロジック（→ [12_GameCore.md](12_GameCore.md)）は座標を持たない
（HP・スタミナ・状態異常のみ）ため、原点移動はリプレイに影響しません。

### 三大規約との関係

- **状態は Tick、艶は Update**: 原点は世界の状態なので判断は `Tick()` で行います
  （デモは `TickPhase.Simulation` に登録し、キャラの移動後に判定します）
- **方針は App**: 閾値・丸め幅・何を追従させるかはゲームの規模で変わるため App が決めます
- **命令の処理者は1基盤**: 原点移動は命令ではなく**通知**です（誰かに実行を頼むのではなく、
  実行した事実を配る）。実行者は `OriginShiftSystem` を持つ App 側です

## 6. よくあるつまずき

- **症状: シフト後にキャラだけ取り残される**
  → 原因: `ActorPose`（純C#の座標）を追従させていない
  → 対処: 追従契約か `OriginShifted` 購読で `Pose.Position += delta` する。
  表示に即反映したいなら `Avatar.ApplyPose` も呼ぶ

- **症状: 一部のオブジェクトが2倍ずれる**
  → 原因: 親子関係にある Transform を両方 `AddRoot` している
  → 対処: 最上位だけを登録する

- **症状: シフトの瞬間にカメラが吹き飛ぶ**
  → 原因: Cinemachine が対象の過去位置を覚えている（瞬間移動を高速移動と誤解する）
  → 対処: `CinemachineCore.OnTargetObjectWarped` を呼ぶ。`CameraDirector` は通知購読で実施済み

- **症状: 足IK が一瞬乱れる**
  → 原因: 足IK の時間追従は前フレームとの差で動くため、ワープ相当の移動で目標が飛ぶ
  → 対処: `FootIkRig.ResetFollow()` を呼ぶ（デモの `ShiftOwnCoordinates` がこの形）

- **症状: 閾値を超えているのにシフトしない**
  → 原因: `SnapSize` が大きすぎて、丸めた結果が 0 になっている
  → 対処: `SnapSize` を小さくするか、`Threshold` を `SnapSize` より大きくする

- **症状: セーブしてロードすると位置がずれる**
  → 原因: 見た目の座標をそのまま保存している（累積量が失われる）
  → 対処: `ToAbsolute` で絶対座標に変換して保存し、ロード時に `ToShifted` で戻す（→ [13_Persistence.md](13_Persistence.md)）

- **症状: パーティクルだけ元の位置に残る**
  → 原因: ワールド空間シミュレーションのパーティクルは親の移動に追従しない
  → 対処: シフト時に `ParticleSystem` を作り直す、またはローカル空間シミュレーションにする

## 7. 増やす・拡張する

| やりたいこと | 手順 |
|---|---|
| 世界の一部を追従させる | `AddRoot(root)` 1行（最上位の Transform だけ） |
| 純C#の座標を追従させる | `IOriginShiftHandler` を実装して `AddHandler`、または `OriginShifted` を購読 |
| テレポート直後に整える | `ShiftNow(delta)` を呼ぶ（閾値を待たずに適用し、累積にも記録される） |
| 高さもずらす（飛行・宇宙） | `Initialize(..., shiftVertical: true)` |
| 模様の位相を保つ | `snapSize` にタイル周期の倍数を渡す |
| セーブと両立させる | 保存は `ToAbsolute`、復元は `ToShifted` を通す |

## 8. 関連ファイルとテスト

- `Assets/Script/World/Runtime/OriginShifter.cs`（判断・純C#）
- `Assets/Script/World/Runtime/OriginShiftSystem.cs`（適用・告知・`IOriginShiftHandler`）
- `Assets/Script/Hub/Contracts/Messages/OriginShifted.cs`（通知の契約）
- `Assets/Script/World/Tests/Editor/OriginShiftTests.cs`（8件。閾値・水平のみ・格子への丸め・
  丸めて 0 なら中止・累積と往復変換・判断が状態を変えないことを仕様として読める）
- `Assets/Script/App/Samples/Sample_BattlePhase.cs` の `BuildOriginShift` /
  `Sample_OriginFollower` / `ShiftOwnCoordinates`
- `Assets/Script/Cameras/Runtime/CameraDirector.cs` の `OnOriginShifted`（Cinemachine への補正）

[← 前: 15_Pooling](15_Pooling.md) | [索引](README.md) | [次: 17_Tests →](17_Tests.md)
