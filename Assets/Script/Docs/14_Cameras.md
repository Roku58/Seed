# 14. Seed.Cameras — カメラ制御（FPS/TPS 切替・複数カメラの合成）

[← 前: 13_Persistence](13_Persistence.md) | [索引](README.md) | [次: 15_Pooling →](15_Pooling.md)

## この章で分かること

- 一人称（FPS）と三人称（TPS）を命令1発で切り替える仕組み
- 演出カメラを一時的に重ね、終わったら元の視点へ必ず戻る2層構造
- カメラの「見え方」を Cinemachine に任せ、Seed 側が「指名の入口」だけを持つ理由
- 複数カメラの合成（ブレンド）がどこで行われているか
- 統合デモで [C] キーを押したときに何が起きているか

## 前提

- [02_Hub.md](02_Hub.md) — 命令（`PublishCommand`）と通知（`Publish`）の区別。視点の切替は命令、切替結果は通知です
- [08_Character.md](08_Character.md) — 追従対象になるキャラクターの表示物（Avatar の Transform）
- [09_Motion.md](09_Motion.md) — 一人称カメラは頭のボーンに固定するため、注視リグの結果がそのまま画面になります

この章が初出の主な用語: Cinemachine / バーチャルカメラ / ブレイン（CinemachineBrain）/ ブレンド / 優先度（Priority）/ 視点ID（ViewpointId）/ 基本の視点と重ねの視点。

## 1. これは何か

`Seed.Cameras` は「どの視点で世界を見せるか」を決める基盤です。カメラの動き自体は
**Cinemachine 3.1.7**（`Packages/manifest.json` の依存に追加済み）が担当し、
本基盤はその上に「視点IDで指名できる」という薄い契約を足します。

> 📖 **用語 — Cinemachine**: Unity 公式のカメラ制御パッケージ。追従の減衰・遮蔽回避・
> 構図の維持・カメラ間のブレンドといった「カメラの手触り」を部品の組み合わせで作れます。
> 本プロジェクトの名前空間は `Unity.Cinemachine`、アセンブリ名は `Unity.Cinemachine` です。

なぜ自作せず Cinemachine を駆動する形にしたのか。追従の減衰・壁との遮蔽回避・複数カメラの
ブレンドは実装量が大きく、しかも Unity 側で年々改良される領域です。ここを自作すると、
品質で負けたうえに保守が増えます。一方で**基盤としての価値は別のところにあります**——
「ゲームの他の部分がカメラの実体を知らずに済む」ことです。UI やイベントが
「一人称に切り替えて」と頼めるようになれば、カメラの作り（距離・画角・追従方式）を
後から総取り替えしても、頼む側のコードは1行も変わりません。

無いと困ることは具体的です。

1. **切替のたびに実体を知る必要がある**。`CinemachineCamera` を UI や戦闘ロジックが直接
   参照して `Priority` を上げ下げすると、カメラの構成を変えるたびに参照元すべてが壊れます
2. **演出が終わったときに戻る先が分からない**。決着の寄りカメラを出したあと、
   プレイヤーが FPS だったか TPS だったかを呼び出し側が覚えておく必要が出ます
3. **原点回帰でカメラが飛ぶ**。Cinemachine は追従の減衰のために対象の過去位置を覚えているため、
   世界をずらしたことを伝えないと「一瞬すごい速度で移動した」と誤解します（→ [16_World.md](16_World.md)）

> 📖 **用語 — 名前空間が `Seed.Cameras`（複数形）な理由**: `Seed.Camera` にすると、
> `using UnityEngine;` した状態で `Camera` と書いたときに「名前空間 `Seed.Camera`」と
> 「型 `UnityEngine.Camera`」が衝突して名前解決が壊れます。複数形にして回避しています
> （「複数カメラの管理」という役割にも合っています）。

## 2. 全体像

### 部品表

| 部品 | 場所 | 役割 |
|---|---|---|
| `ViewpointId` | `Assets/Script/Hub/Contracts/ViewpointId.cs` | 境界を越えて視点を指す共有ID（0=None / 1=FirstPerson / 2=ThirdPerson / 3=Overhead / 4=Cutscene、100以降がアプリ独自） |
| `SetViewpointCommand` / `PushViewpointCommand` / `PopViewpointCommand` / `ViewpointChanged` | `Assets/Script/Hub/Contracts/Messages/SetViewpointCommand.cs` | 切替の命令3種と結果の通知1種 |
| `ViewpointStack` | `Assets/Script/Cameras/Runtime/ViewpointStack.cs` | 純C#。どの視点が今有効かを決める判断者 |
| `CameraDirector` | `Assets/Script/Cameras/Runtime/CameraDirector.cs` | MonoBehaviour。視点ID→カメラの登録表・命令の処理者・Cinemachine への反映 |
| `CameraRigBuilder` | `Assets/Script/Cameras/Runtime/CameraRigBuilder.cs` | 定番カメラ構成（一人称・三人称・固定）の組み立て補助 |
| `CinemachineBrain` | Cinemachine | 実カメラに付き、有効なバーチャルカメラの絵を焼き付ける。**合成（ブレンド）の担当** |
| `CinemachineCamera` | Cinemachine | バーチャルカメラ本体。位置と向きの作り方を部品で決める |

> 📖 **用語 — バーチャルカメラ / ブレイン**: Cinemachine では、実際に描画する `Camera` は1つだけ置き、
> 「どこから何を狙うか」の候補を `CinemachineCamera`（バーチャルカメラ）として複数用意します。
> `CinemachineBrain` が優先度の一番高い候補を選び、切り替わるときは前の絵から新しい絵へ
> 補間します。これが「複数カメラの合成」の実体です。

### データの流れ

```
UI・戦闘ロジック・入力（どの基盤でもよい）
   │ hub.PublishCommand(new SetViewpointCommand(ViewpointId.FirstPerson, 0.35f))
   ▼
CameraDirector（命令の唯一の処理者）
   ├─ ViewpointStack が「有効な視点」を決める（基本 or 重ねの最前面）
   ├─ 変わったときだけ: ブレンド秒数を Brain へ反映 → 各カメラの Priority を更新
   └─ hub.Publish(new ViewpointChanged(previous, current))
        ▼
CinemachineBrain（Camera に付いている）
   └─ 優先度の一番高いバーチャルカメラへ、指定の秒数でブレンドしながら切り替える
```

> 📖 **用語 — 優先度（Priority）**: バーチャルカメラの「選ばれやすさ」。ブレインは有効なカメラの中で
> 最も高いものを選びます。`CameraDirector` は有効な視点のカメラだけを 100、他を 0 にすることで
> 指名を表現します（Cinemachine 3 では `int` から暗黙変換できるため `camera.Priority = 100` と書けます）。

### 命令と通知の使い分け

| したいこと | 使うもの | 効果 |
|---|---|---|
| 常用の視点を変える（FPS ⇄ TPS） | `SetViewpointCommand` | 基本の視点を差し替える |
| 演出カメラを一時的に出す | `PushViewpointCommand` | 上へ重ねる（基本は保持される） |
| 演出を終える | `PopViewpointCommand` | 重ねを外す（基本へ戻る） |
| 視点が変わったことを知る | `ViewpointChanged` を購読 | HUD の切替・解析ログ |

## 3. 動かして試す

統合デモ（[01_Demo.md](01_Demo.md)）の戦闘フェーズがそのまま実演になっています。

1. 空の GameObject に `Sample_GameFlowRunner` を付けて **Play**
2. **[1]** キーで草原へ出撃する。最初は**三人称（肩越し）**で、青カプセルの後ろから見た画面になります
3. **[C]** キーを押す → **一人称**へ 0.35 秒でブレンドしながら切り替わります。
   頭に固定されるので、注視リグが敵を追って頭が回ると画面も一緒に回ります
4. もう一度 **[C]** → **俯瞰**（固定カメラ。ステージ全体が見える）
5. さらに **[C]** → 三人称へ戻る（巡回）

期待される結果:

- 切り替わりは瞬間ではなく**補間**されます（`CinemachineBrain.DefaultBlend` が効いている）
- Hierarchy の `Stage_Grassland > CameraRigs` の下に `Viewpoint_TPS` / `Viewpoint_FPS` /
  `Viewpoint_Overhead` の3つが並び、**選ばれているものだけ Priority が 100**、他は 0 になります
  （Inspector で確認できます）
- `Main Camera` に `CinemachineBrain` が付いています（`CameraRigBuilder.EnsureBrain` が付けたもの）
- WASD で移動すると三人称カメラが少し遅れて付いてきます（`CinemachineThirdPersonFollow` の減衰）

## 4. コードで使う

### 最小例 — 視点を2つ登録して切り替える

```csharp
using Seed.Cameras;
using Seed.Hub.Contracts;
using UnityEngine;

// 1. 実カメラにブレインを用意する（合成の担当）
var brain = CameraRigBuilder.EnsureBrain(Camera.main, defaultBlendSeconds: 0.4f);

// 2. バーチャルカメラを2つ作る（定番構成の組み立て補助を使う）
var tps = CameraRigBuilder.CreateThirdPerson("Viewpoint_TPS",
    distance: 4.5f, shoulderSide: 0.5f, height: 1.2f);
var fps = CameraRigBuilder.CreateFirstPerson("Viewpoint_FPS", fieldOfView: 80f);

// 3. 采配役へ登録する（命令の処理者になる）
var director = gameObject.AddComponent<CameraDirector>();
director.Initialize(hub, brain);
director.Register(ViewpointId.ThirdPerson, tps)
        .Register(ViewpointId.FirstPerson, fps);

// 4. 追従対象を一括で差し替える（登録した全カメラに効く）
director.SetTarget(playerPivot);

// 5. 以後、切替は命令1発（発行側はカメラの実体を知らない）
hub.PublishCommand(new SetViewpointCommand(ViewpointId.FirstPerson, blendSeconds: 0.35f));
```

> 📖 **用語 — TrackingTarget / LookAtTarget**: Cinemachine のカメラが追う対象。
> `TrackingTarget` は位置の追従先、`LookAtTarget` は向きの狙い先です。両方を分けたいときだけ
> `CustomLookAtTarget` を true にします（分けない場合は `TrackingTarget` が両方に使われます）。

> 📖 **用語 — 肩越し追従（ThirdPersonFollow）**: 対象の肩の後ろにカメラを置く三人称の定番構成。
> `ShoulderOffset`（左右と高さ）・`CameraDistance`（距離）・`Damping`（軸ごとの追従の粘り）で
> 手触りを決めます。壁との遮蔽回避も内蔵しています。

### 実戦例 — 追従対象を視点ごとに変える（デモの構成）

一人称は「頭」に固定し、三人称は「腰の高さのダミー」を追わせたい、という場合。
`SetTarget` は `followsTarget: true` で登録したカメラだけに効くので、
個別に設定したいものは `false` で登録しておきます。

```csharp
var director = rigRoot.gameObject.AddComponent<CameraDirector>();
director.Initialize(_hub, brain);
director
    .Register(ViewpointId.ThirdPerson, thirdPerson)                        // 一括差し替えの対象
    .Register(ViewpointId.FirstPerson, firstPerson, followsTarget: false)  // 個別に設定する
    .Register(ViewpointId.Overhead, overhead, followsTarget: false);       // 固定カメラ

// 三人称だけが腰のダミーを追う
director.SetTarget(_cameraPivot);

// 一人称は頭に固定（首の動きがそのまま画面になる）
var fpsTarget = firstPerson.Target;
fpsTarget.TrackingTarget = _playerHead;
firstPerson.Target = fpsTarget;

// フェーズの初期視点は命令を経由せず直接指定してよい（ブレンド0＝カット）
director.SetBase(ViewpointId.ThirdPerson, blendSeconds: 0f);
```

### 実戦例 — 演出カメラを重ねて、終わったら戻す

```csharp
// 決着の寄り: 今の視点が FPS でも TPS でも、演出後は元の視点へ必ず戻る
_hub.PublishCommand(new PushViewpointCommand(ViewpointId.Cutscene, blendSeconds: 0.6f));

// …演出が終わったら
_hub.PublishCommand(new PopViewpointCommand(ViewpointId.Cutscene, blendSeconds: 0.4f));
```

## 5. 仕組み

### 2層構造（基本の視点と重ねの視点）

カメラの切替には性質の違う2種類があります。プレイヤーが選ぶ**常用の視点**（FPS/TPS）と、
イベントが一時的に奪う**演出の視点**（決着の寄り・照準）です。これを同じ仕組みで扱うと、
演出が終わったときに「元がどっちだったか」を呼び出し側が覚えておく必要が出ます。

`ViewpointStack` は基本（`Base`）と重ね（`Overlay`）を分けて持ち、有効な視点は
「重ねがあれば最前面、無ければ基本」と決めます。演出は積んで外すだけで元へ戻ります。

重ねの取り下げは**順不同**にできます（`Pop(ViewpointId)` で指定を抜く）。
照準中に決着した、のように演出が入れ違って終わっても破綻しないためです。
積まれていない視点の `Pop` は無害に無視されるので、二重終了にも強くなっています。

### 変化しない操作ではブレンドを起こさない

`ViewpointStack` の各操作は「有効な視点が変わったか」を bool で返します。
`CameraDirector` はそれが true のときだけ Cinemachine へ反映します。
重ねに隠れている状態で基本を切り替えても見え方は変わらないので、
無用なブレンドが起きません（取り下げた瞬間に新しい基本が現れます）。

### ブレンド秒数の指定

命令の `BlendSeconds` は次のように解釈されます。

| 値 | 意味 |
|---|---|
| 負値（既定 -1） | 既定のブレンドをそのまま使う（何も書き換えない） |
| 0 | カット（`CinemachineBlendDefinition.Styles.Cut`） |
| 正値 | その秒数で `EaseInOut` ブレンド |

指定は `CinemachineBrain.DefaultBlend` の書き換えとして反映され、**次に指定されるまで維持されます**。
Cinemachine のブレンドは遷移開始時に確定するため、直後に元へ戻すと進行中のブレンドが崩れるからです。

> 📖 **用語 — カット（Cut）**: 補間せず1フレームで切り替える演出。`BlendSeconds` に 0 を渡すと
> これになります。フェーズ入場時の初期視点や、意図的に切り替えを見せたい演出で使います。

### 三大規約との関係

- **命令の処理者は1基盤**: `SetViewpointCommand` などの処理者は `CameraDirector` 1つだけです。
  2つ Initialize すると Hub が二重処理者を検知して例外になります（配線ミスの即時検出）
- **方針は App**: 「いつ・どの視点にするか」は App（`Sample_BattlePhase` の [C] 分岐や
  決着時の演出）が決めます。基盤は「指名できる仕組み」だけを持ちます
- **状態は Tick、艶は Update**: カメラは艶の側です。`CameraDirector` は毎フレームの
  Tick を持たず、命令を受けたときだけ動きます。毎フレームの補間は Cinemachine の担当です

### 未登録の視点は即例外

`Register` していない `ViewpointId` へ切り替えようとすると `HubException` になります
（「未登録の視点（Register 漏れかIDの打ち間違い）」）。
黙って無視すると「切り替わらない理由が分からない」状態が続くため、発生点で落とします。

## 6. よくあるつまずき

- **症状: 切り替えても画面が変わらない**
  → 原因: `Main Camera` に `CinemachineBrain` が無い（バーチャルカメラは絵の候補にすぎず、
  ブレインが無いと実カメラへ反映されない）
  → 対処: `CameraRigBuilder.EnsureBrain(Camera.main)` を1回呼ぶ

- **症状: `HubException`「未登録の視点」が出る**
  → 原因: `Register` 漏れ、または `ViewpointId` の値の打ち間違い
  → 対処: 登録した視点IDと命令の視点IDを突き合わせる

- **症状: 一人称にすると自分の体が画面を覆う**
  → 原因: 頭に固定したカメラの内側にモデルが入っている
  → 対処: 一人称中はモデルの描画を切る、またはカメラのニアクリップを調整する（App の方針）

- **症状: 追従対象を差し替えたのに一部のカメラが古い対象を追う**
  → 原因: そのカメラを `followsTarget: false` で登録している（`SetTarget` の対象外）
  → 対処: 個別に `camera.Target` を設定する（デモの一人称カメラがこの形）

- **症状: 原点回帰の直後にカメラが吹き飛ぶ**
  → 原因: Cinemachine が対象の過去位置を覚えているため、瞬間移動を高速移動と誤解する
  → 対処: `CameraDirector` は `OriginShifted` 通知を購読して
  `CinemachineCore.OnTargetObjectWarped` を呼ぶようになっている。自作の追従を書くときは同様の通知が必要
  （→ [16_World.md](16_World.md)）

- **症状: 演出カメラが終わっても戻らない**
  → 原因: `Pop` を呼んでいない、または別の視点を `Pop` している
  → 対処: フェーズ退場時など確実に戻したい場面では `ClearOverlays()` を使う

## 7. 増やす・拡張する

| やりたいこと | 手順 |
|---|---|
| 視点を1つ増やす | `ViewpointId` をアプリ定数として発番（100以降）→ `CinemachineCamera` を作る → `Register` 1行 |
| 追従の手触りを変える | Cinemachine の部品を差し替える（`CinemachineOrbitalFollow`・`CinemachinePositionComposer` など）。Seed 側の変更は不要 |
| ブレンド曲線を凝る | `CinemachineBrain.CustomBlends`（`CinemachineBlenderSettings`）でカメラ対ごとの曲線を指定する |
| 手ぶれ・衝撃 | 対象カメラに `CinemachineBasicMultiChannelPerlin` を足す（基盤の変更なし） |
| 照準カメラ | `PushViewpointCommand` で重ね、離したら `Pop` する（基本の視点は保持される） |
| プレイヤーの乗り換え | `director.SetTarget(newPivot)` 1行 |

視点の実体をプレハブで持つ運用にする場合は、`CameraRigBuilder` を使わずに
プレハブを Instantiate して `Register` するだけです（組み立て補助は必須ではありません）。

## 8. 関連ファイルとテスト

- `Assets/Script/Cameras/Runtime/ViewpointStack.cs`（判断・純C#）
- `Assets/Script/Cameras/Runtime/CameraDirector.cs`（登録表・命令の処理者・反映）
- `Assets/Script/Cameras/Runtime/CameraRigBuilder.cs`（定番構成の組み立て）
- `Assets/Script/Hub/Contracts/ViewpointId.cs` / `Messages/SetViewpointCommand.cs`（契約）
- `Assets/Script/Cameras/Tests/Editor/CameraTests.cs`（10件。2層構造・順不同の取り下げ・
  二重積みの扱い・変化しない操作の判定を仕様として読める）
- `Assets/Script/App/Samples/Sample_BattlePhase.cs` の `BuildCameras`（デモの組み立て）

[← 前: 13_Persistence](13_Persistence.md) | [索引](README.md) | [次: 15_Pooling →](15_Pooling.md)
