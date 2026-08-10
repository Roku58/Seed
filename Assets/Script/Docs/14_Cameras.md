# 14. Seed.Cameras — カメラ制御（FPS/TPS 切替・複数カメラの管理と合成）

[← 前: 13_Persistence](13_Persistence.md) | [索引](00_Roadmap.md) | [次: 15_Pooling →](15_Pooling.md)

## この章で分かること

- 視点ID（`ViewpointId`）でカメラを指名し、命令1発で FPS/TPS/俯瞰を切り替える方法
- 「基本の視点」と「重ねの視点（演出カメラ）」の2層構造と、その必要性
- Cinemachine との役割分担——なぜ追従・ブレンドを自作しないのか
- デモでの試し方（[C] キー巡回）と、追従対象の差し替え

## 前提

- [02_Hub.md](02_Hub.md) — 命令（処理者1基盤）と通知の使い分け
- [01_Demo.md](01_Demo.md) — 戦闘フェーズの起動

この章が初出の主な用語: バーチャルカメラ / ブレンド / 優先度 / Cinemachine Brain。

## 1. これは何か

UI やイベントが「一人称に切り替えて」と頼むとき、カメラの作り（追従方式・レンズ・
ブレンド曲線）まで知る必要はありません。`Seed.Cameras` は **視点ID → カメラの登録表**と
**切替命令の処理者**だけを提供し、「どう見えるか」は Cinemachine の資産に委ねます。

無いと困ること:

- 各所が `Camera.main.transform` を直接触り、切替・演出・原点回帰が衝突する
- 「演出カメラが終わったら元の視点へ戻す」を呼び出し側が覚えておく羽目になる
- FPS/TPS の切替のたびに追従・遮蔽回避・ブレンドを自作することになる

> 📖 **用語 — Cinemachine**: Unity 公式のカメラ制御パッケージ（本プロジェクトは 3.1.7）。
> 「バーチャルカメラ」（構図の定義）を複数置き、**優先度が最も高いものの絵**を実カメラへ
> 焼き付ける。切替時は自動でブレンド（補間）される。追従の減衰・肩越しの衝突回避・
> 手ぶれといった「見え方」の実装量が大きく、Unity 側で改良され続ける領域のため、
> Seed はここを自作せず駆動に徹する。

## 2. 全体像

| 部品 | 場所 | 役割 |
|---|---|---|
| `ViewpointId` | `Assets/Script/Hub/Contracts/ViewpointId.cs` | 視点の共有ID（FirstPerson=1 / ThirdPerson=2 / Overhead=3 / Cutscene=4。アプリ独自100+） |
| `SetViewpointCommand` ほか | `Hub/Contracts/Messages/SetViewpointCommand.cs` | 切替（Set）・重ね（Push）・取り下げ（Pop）の命令と `ViewpointChanged` 通知 |
| `ViewpointStack` | `Assets/Script/Cameras/Runtime/ViewpointStack.cs` | どの視点が有効かの判断者（純C#・テスト済み） |
| `CameraDirector` | `Cameras/Runtime/CameraDirector.cs` | 命令の唯一の処理者。登録表と Cinemachine への反映 |
| `CameraRigBuilder` | `Cameras/Runtime/CameraRigBuilder.cs` | 一人称・三人称・固定カメラの定番構成をコード1行で組む補助 |

```
App「[C] が押された」
   │ hub.PublishCommand(new SetViewpointCommand(FirstPerson, 0.35f))
   ▼
CameraDirector（処理者1基盤）
   ├ ViewpointStack が有効視点を判断（基本 or 最前面の重ね）
   ├ 有効なカメラの Priority だけ上げる（他は0）
   └ ViewpointChanged を通知（HUD の照準表示切替などが購読）
   ▼
Cinemachine Brain が優先カメラの絵へブレンドして実カメラに焼く（合成）
```

### 2層構造（基本＋重ね）の理由

カメラ切替には性質の違う2種類があります——プレイヤーが選ぶ**常用の視点**（FPS/TPS）と、
イベントが一時的に奪う**演出の視点**（決着の寄り・照準）。1本のスタックで扱うと
「演出後に元がどっちだったか」を呼び出し側が覚える必要が出ます。基本（`SetBase`）と
重ね（`Push`/`Pop`）に分ければ、**演出は積んで外すだけで必ず元の視点へ戻ります**。
取り下げは順不同でも破綻しません（演出が入れ違って終わっても安全）。

## 3. 動かして試す

1. 統合デモを起動し **[1] で草原へ出撃**（→ [01_Demo.md](01_Demo.md)）
2. **[C] を押す**たびに 三人称（肩越し）→ 一人称 → 俯瞰 → 三人称… と 0.35 秒のブレンドで巡回
3. 一人称では頭のボーンに固定されるため、**注視リグ（09章）が敵を向く動きがそのまま画面が向く動き**になる
4. デバッグメニュー（エディタ Play 中に自動表示 → [18_Libraries.md](18_Libraries.md)）の
   「カメラ」ボタンからも同じ命令を発行できる
5. Hierarchy では `Stage_Grassland/CameraRigs` の下に `Viewpoint_TPS / Viewpoint_FPS / Viewpoint_Overhead`
   が並び、Main Camera に `CinemachineBrain` が付いている

## 4. コードで使う

### 最小例 — 切り替えは命令1発

```csharp
// どの基盤からでも。発行側はカメラの実体を知らない
_hub.PublishCommand(new SetViewpointCommand(ViewpointId.FirstPerson, blendSeconds: 0.35f));

// 演出カメラを重ねて、終わったら外す（元の視点へ自動で戻る）
_hub.PublishCommand(new PushViewpointCommand(ViewpointId.Cutscene, 0.5f));
_hub.PublishCommand(new PopViewpointCommand(ViewpointId.Cutscene));
```

### 実戦例 — フェーズでカメラを組む（`Sample_BattlePhase.BuildCameras` の骨格）

```csharp
var brain = CameraRigBuilder.EnsureBrain(Camera.main, defaultBlendSeconds: 0.4f);
var tps = CameraRigBuilder.CreateThirdPerson("Viewpoint_TPS", rigRoot, distance: 4.5f);
var fps = CameraRigBuilder.CreateFirstPerson("Viewpoint_FPS", rigRoot, fieldOfView: 80f);
var overhead = CameraRigBuilder.CreateFixed("Viewpoint_Overhead",
    new Vector3(0f, 13f, -9f), Vector3.zero, rigRoot);

_cameraDirector = rigRoot.gameObject.AddComponent<CameraDirector>();
_cameraDirector.Initialize(_hub, brain);       // 命令3種の処理者になる
_cameraDirector
    .Register(ViewpointId.ThirdPerson, tps)
    .Register(ViewpointId.FirstPerson, fps, followsTarget: false)  // FPS は個別に頭へ固定
    .Register(ViewpointId.Overhead, overhead, followsTarget: false);
_cameraDirector.SetTarget(cameraPivot);        // 追従対象の一括差し替え
_cameraDirector.SetBase(ViewpointId.ThirdPerson, blendSeconds: 0f);
```

`SetTarget` は「乗り物への乗り換え」「ステージ再入」で追従対象をまとめて差し替えるための
入口です。一人称だけは `followsTarget: false` で外し、頭ボーンへ個別に固定しています
（腰のダミーを追う三人称と、頭に張り付く一人称で追従先が違うため）。

## 5. 仕組み

- **判断（ViewpointStack・純C#）と反映（CameraDirector）を分離**——切替の全パターン
  （重ね中の基本切替・順不同の取り下げ・二重 Push）は EditMode テスト10件で検証済み
- 反映は「有効な視点の `Priority` を100へ、他を0へ」だけ。**合成（ブレンド）は
  Cinemachine Brain の仕事**で、`blendSeconds` 指定は Brain の既定ブレンドを書き換えて実現
- 未登録の視点への切替は即 `HubException`（配線漏れをその場で検出——Hub の規約と同じ）
- 原点回帰（→ [16_World.md](16_World.md)）が起きると `CinemachineCore.OnTargetObjectWarped`
  で追従履歴を補正する。これが無いと「一瞬で数十m移動した」と誤解したカメラが飛ぶ
- 名前空間が `Seed.Cameras`（複数形）なのは、`Seed.Camera` だと `UnityEngine.Camera` 型と
  名前解決が衝突するため

## 6. よくあるつまずき

- **症状**: 切替命令で `HubException`（処理者不在） → **原因**: `CameraDirector.Initialize` を
  呼んでいない（戦闘フェーズ外） → **対処**: カメラを使うフェーズの合成ルートで組む
- **症状**: 切り替わるがブレンドせず一瞬で切り替わる → **原因**: `EnsureBrain` を通していない
  （Brain が無い） → **対処**: 実カメラに CinemachineBrain を付ける
- **症状**: 一人称で足元や体内が見える → **原因**: 追従先が頭ではなく root →
  **対処**: FPS カメラの `Target.TrackingTarget` を頭ボーンへ
- **症状**: 演出カメラの後に視点がおかしい → **原因**: `Pop` 漏れ →
  **対処**: フェーズ退場では `ClearOverlays()` で強制的に基本へ戻せる

## 7. 増やす・拡張する

- 視点の追加 = `ViewpointId` をアプリ定数（100+）で発番 → CinemachineCamera を組んで
  `Register` 1行 →好きな場所から `SetViewpointCommand`
- 構図の調整 = Cinemachine のコンポーネント（ThirdPersonFollow の距離・肩オフセット等）を
  そのまま触る（Seed 側の関与なし）
- カメラ揺れ = `CinemachineBasicMultiChannelPerlin` を該当カメラへ足す（被弾時に
  振幅を上げる方針コードは App 側へ）

## 8. 関連ファイルとテスト

- `Assets/Script/Cameras/Runtime/`（3ファイル）・`Assets/Script/Hub/Contracts/ViewpointId.cs`
- デモ統合: `Assets/Script/App/Samples/Sample_BattlePhase.cs` の `BuildCameras`
- テスト: `Assets/Script/Cameras/Tests/Editor/CameraTests.cs`（視点スタック10件）

[← 前: 13_Persistence](13_Persistence.md) | [索引](00_Roadmap.md) | [次: 15_Pooling →](15_Pooling.md)
