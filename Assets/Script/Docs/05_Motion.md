# 05. Seed.Motion — アニメ再生・姿勢・IK・揺れもの

Behavior 遷移（真実）をアニメ表現へ翻訳する層。AnimatorController アセット不要
（Playables 直駆動）。姿勢・IK・揺れものは MotionRig（LateUpdate）でアニメの上に重ねる「艶」。

## デモで試す（統合デモの戦闘フェーズ・プレイヤーに装着済み）

- **注視**: 頭キューブ（黒い鼻つき）が常に敵の胸元を向く（LookAtRig・最大70度）。敵死亡で解除
- **腕IK**: 敵と 3m 未満で左腕（Shoulder→Elbow→Hand）が敵へ伸びる。近いほど強く（TwoBoneIkRig）
- **揺れもの**: 後頭部のポニーテール4関節が移動・旋回・注視に遅れて揺れ、頭にめり込まない
  （SpringBoneRig・頭に球コライダー半径0.22）
- リグの適用順は登録順: 注視 → 腕IK → 揺れ（揺れが最後＝注視で動いた頭に追従する）

## 実モデルで使う（RiggedAvatar）

```csharp
using Seed.Character;
using Seed.Motion;
using UnityEngine;

// モデルの GameObject に Animator を残したまま（Controller アセットは不要）
var avatar = model.AddComponent<RiggedAvatar>();

// クリップ台帳: モーション追加 = Add 1行。‰イベントで当たり判定窓もデータ登録
var motions = new MotionSet()
    .Add(MotionClipId.Idle, idleClip)
    .Add(MotionClipId.Locomotion, runClip)
    .Add(MotionClipId.Attack, slashClip, fadeSeconds: 0.08f, speed: 1f, loop: false,
        new MotionEvent(300, AvatarEventId.HitboxBegin),   // 30% 地点で判定開始
        new MotionEvent(600, AvatarEventId.HitboxEnd));

avatar.Configure(animator, motions);  // 上半身レイヤーは第4引数の AvatarMask
// あとは CharacterFactory.Create の ActorBlueprint に渡すだけ。
// BehaviorKey と MotionClipId の番号を揃えれば対応表（MotionBindings）は不要（同値素通し）
```

姿勢・IK・揺れものの装着（どの Avatar でも可）:

```csharp
var rig = avatar.gameObject.AddComponent<MotionRig>()
    .With(new LookAtRig((headBone, 1f, 70f)))                       // 注視
    .With(new TwoBoneIkRig(shoulder, elbow, hand, poleHint))        // 腕IK
    .With(new FootIkRig(hips, leftLeg, rightLeg, groundMask))       // 接地
    .With(new SpringBoneRig(tailBones, colliders: new[] { (head, 0.22f) })); // 揺れもの
```

## 部品の対応表

| 部品 | 役割 |
|---|---|
| AnimationDriver | Playables 直駆動のクロスフェード再生（全身＋上半身の2レイヤー） |
| MotionSet / MotionClipId | クリップ台帳（再生仕様＋正規化‰イベント） |
| RiggedAvatar | IAvatar 実装。Behavior 遷移→クロスフェード、イベント→行動側へ還流 |
| LookAtRig / TwoBoneIkRig / ChainIkRig / FootIkRig | 注視 / 2ボーンIK / FABRIK / 接地 |
| SpringBoneRig / SpringBoneChain | 揺れもの（Verlet・固定1/60秒刻み・球コライダー押し出し） |
| RootMotionRelay | ルートモーション捕獲（`TakeDelta()` を Tick 側が消費） |

## ハマりどころ

- RiggedAvatar は生成直後に `Configure` を1回呼ぶこと（呼ばないと遷移しても無反応）
- MotionSet 未登録の MotionClipId は静かに何もしない（例外にしない設計）
- ‰イベントは全身レイヤーのみ発火（上半身レイヤーは発火しない）
- **揺れボーン列はアニメクリップで焼かない**（素姿勢=装着時のローカル回転。毎フレーム復元される）
- SpringBoneChain は固定 1/60 秒刻みのアキュムレータ式（FPS 非依存の質感）。
  1フレームの消化上限 0.1 秒、根本 2m 超の瞬間移動で自動リセット（鞭化防止）
- Weight=0 の意味はリグごとに違う: IK 系は何もしない / SpringBoneRig はシミュだけ進める
- ルートモーションは Transform へ直接適用しない（RootMotionRelay で貯めて Tick 側が消費
  ＝壁判定・リプレイと矛盾しない）

## 増やすとき

- モーション追加 = `MotionSet.Add` 1行（番号がズレる時だけ `MotionBindings.Bind` 1行）
- 独自リグ = `IPoseRig` 実装（Weight + Apply()）→ `MotionRig.With`
- 揺れの質感 = `SpringBoneParams`（Stiffness/Drag/Gravity/JointRadius。既定は髪向け）

主要ファイル: `Assets/Script/Motion/Runtime/`（RiggedAvatar / AnimationDriver / Rig/ / Spring/ / Ik/）。
テスト: `Assets/Script/Motion/Tests/Editor/MotionTests.cs`（IK 解析解・‰イベント・揺れの数値検証）
