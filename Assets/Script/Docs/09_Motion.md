# 09. Seed.Motion — アニメ・IK・揺れもの

[← 前: 08_Character](08_Character.md) | [索引](README.md) | [次: 10_AI →](10_AI.md)

## この章で分かること

- AnimatorController アセットを使わず、コードとデータだけでアニメーションを再生する方法（Playables 直駆動）
- 当たり判定の窓・コンボ受付をクリップ非編集で定義する「正規化‰イベント」の仕組み
- アニメの上に注視・IK・揺れものを重ねる MotionRig（LateUpdate の「艶」）の使い方と適用順の意味
- 揺れもの（SpringBoneChain）が FPS に依存しない理由——固定 1/60 秒刻みのアキュムレータ式シミュレーション
- 実 3D モデルへ RiggedAvatar とリグ一式を装着する実戦手順

## 前提

先に [08_Character](08_Character.md) を読んでください。この章は次の知識を使います。

- **Behavior 状態機械**: 行動の真実（Idle/Locomotion/Attack…）は `Seed.Character` 側が持つ。本章のアニメはその「写し」
- **IAvatar**: 表示物の契約。本章の RiggedAvatar はその実装のひとつ
- **BehaviorKey / AvatarEventId**: 行動キーとアニメイベント ID の値体系（標準 1〜99、アプリ独自 100 以降）

この章が初出の主な用語: Playables、クロスフェード、正規化‰イベント、AvatarMask、2ボーンIK、FABRIK、Verlet、素姿勢、ルートモーション。

## 1. これは何か

Seed.Motion は「Behavior 遷移（真実）をアニメ表現へ翻訳する層」です。08 章のデモはプリミティブ演出（踏み込み・色変え）で動きましたが、実モデルでゲームを作るにはクリップ再生・当たり判定タイミング・姿勢の艶（注視・接地・揺れ）が要ります。それを担うのがこの基盤です。

無いと困ることは 3 つあります。

1. **状態機械の二重化**。Unity 標準の AnimatorController でアニメを組むと、「今どの状態か」を Behavior 状態機械と Animator の両方が持ってしまい、遷移条件の手合わせ地獄になります。本基盤は Playables 直駆動で「今このクリップを何％で流す」だけをコードが握り、真実は Behavior 側に一本化します。
2. **当たり判定のタイミング管理**。「攻撃の有効フレームはアニメの 30%〜60%」という情報を秒数のマジックナンバーで持つと、クリップ差し替えで即壊れます。本基盤は正規化‰イベントとしてデータ登録し、アニメ側の真実で行動側を駆動します。
3. **姿勢の艶**。注視・腕IK・接地・揺れものはアニメクリップに焼けません（相手の位置や地形は実行時にしか分からない）。MotionRig が LateUpdate でアニメの上へ重ねます。

> 📖 **用語 — Playables**: Unity の低レベルアニメーション再生 API。クリップやミキサーをノードとして `PlayableGraph` に接続し、再生・ブレンドをコードで完全制御します。AnimatorController（後述）が「アセットとして作る状態機械」なのに対し、Playables は「状態を持たない再生装置」で、何をいつ流すかは呼び出し側が決めます。

> 📖 **用語 — AnimatorController**: Unity 標準のアニメーション状態機械アセット。ステートと遷移矢印を GUI で編集します。本プロジェクトでは**使いません**——遷移の真実は Behavior 状態機械が既に持っており、重ねると真実が二重化するためです。Animator コンポーネント自体は（ボーンへの書き込み口として）必要です。

## 2. 全体像

### 部品表

| 部品 | 役割 |
|---|---|
| AnimationDriver | Playables 直駆動の再生器（全身＋上半身の2レイヤー、各2スロットのクロスフェード） |
| MotionSet / MotionClipId | クリップ台帳（再生仕様＋正規化‰イベント）とモーションの論理名 |
| MotionDescriptor / MotionEvent | 再生仕様の純データ（長さ・ループ・イベント列。テスト可能） |
| CrossfadeState | フェード重みとイベント発火の状態機械（純C#） |
| RiggedAvatar / MotionBindings | IAvatar 実装。行動遷移→クロスフェード翻訳と、行動キー→モーションの対応表 |
| MotionRig / IPoseRig | リグ統括（登録順に LateUpdate で適用） |
| LookAtRig / TwoBoneIkRig / ChainIkRig | 注視 / 2ボーンIK / FABRIK |
| FootIkRig | 足の接地適応（階段・段差・坂）。地面問い合わせと解決器を束ねる |
| IGroundProbe / GroundHit / PhysicsGroundProbe | 地面問い合わせの契約と Physics 実装（差し替え可能） |
| FootPlacementSolver / FootIkSettings | 接地の解き方（純C#。段差・傾斜の上限と時間追従） |
| SpringBoneRig / SpringBoneChain / SpringBoneParams | 揺れもの（Verlet・固定 1/60 秒刻み・球コライダー押し出し） |
| RootMotionRelay | ルートモーション捕獲（`TakeDelta()` / `TakeRotation()` を Tick 側が消費） |

### データの流れ

```
Behavior 状態機械（真実・Tick）
   │ OnBehaviorChanged(previous, next)
   ▼
RiggedAvatar ── MotionBindings.Resolve ──▶ MotionClipId
   │ Driver.Play(clip)
   ▼
AnimationDriver（PlayableGraph・RiggedAvatar.Update が Tick）
   ├─ レイヤー0: 全身（2スロットのクロスフェード）
   │     └─ 正規化‰イベント ─▶ IAvatarEventSink ─▶ 行動側へ還流（唯一の逆流）
   └─ レイヤー1: 上半身（AvatarMask・イベント発火なし）
   ▼ グラフ評価でボーンの Transform へ書き込み
MotionRig（LateUpdate・登録順に適用）
   ├─ LookAtRig / TwoBoneIkRig / FootIkRig …（アニメ姿勢の上書き）
   └─ SpringBoneRig ─▶ SpringBoneChain（固定 1/60 秒刻みシミュレーション）
```

## 3. 動かして試す

統合デモの戦闘フェーズで、プレイヤーにリグ一式が装着済みです（モデルアセット不要のプリミティブ構成）。

1. Unity で任意のシーンを開き、Hierarchy で空の GameObject を作成
2. Add Component で `Sample_GameFlowRunner` を追加（カメラ・ライトは無ければ自動生成）
3. Play を押す。ホーム画面（テキストパネル）が出る
4. **[1] キー**で「出撃: 草原（敵の攻撃: ゆっくり）」→ 戦闘フェーズへ。プレイヤー＝青カプセル、敵＝灰キューブが現れる
5. Hierarchy のプレイヤー配下を確認: `Head`（HeadCube＋黒い Nose）、`Shoulder → Elbow → Hand` の小キューブ列、`Tail0 〜 Tail3` のポニーテールが生成されている
6. **[W][A][S][D]** で移動・旋回しながら観察:
   - **注視**: 頭キューブ（黒い鼻つき）が常に敵の胸元（敵位置＋上 0.5m）を向く。最大回頭角 70 度。敵死亡で解除されアニメ姿勢へ戻る
   - **腕IK**: 敵との距離 3m 未満で左腕（Shoulder→Elbow→Hand）が敵（＋上 0.6m）へ伸びる。近いほど強い（Weight = 1 − 距離/3）。肘は背中側の poleHint へ曲がる
   - **揺れもの**: ポニーテール 4 関節が移動・旋回・注視に遅れて揺れ、頭の球コライダー（半径 0.22）にめり込まない
7. **[1]** で攻撃、**[G]** でガード、**[P]** でポーズ、**[B]** でホームへ帰還

Console に特別なログは出ません（構成ミス時のみ HubException）。リグの適用順は登録順で **注視 → 腕IK → 揺れ** ——揺れが最後だから、注視で動いた後の頭にポニーテールが追従します。

なおデモのリグ装着（`Sample_BattlePhase.AttachDemoMotionRig`）は Avatar3D 用です。RiggedAvatar はデモでは未使用で、実モデル向けの本命実装です（次節）。

> 📖 **実モデルでの実例**: デモは UnityChan（`Assets/UnityChan`）を使います。
> `Sample_PlayerModel`（`Assets/Script/App/Samples/Sample_PlayerModel.cs`）が RiggedAvatar＋
> クリップ台帳（Idle/Locomotion/Attack/Guard/Hit/Death の6種）を組み、注視・腕IK・足IKを
> `GetBoneTransform` の Humanoid ボーンへ、**揺れもの（髪・リボン・スカート・袖）を
> SpringBoneRig へ**装着します。初回はメニュー `Seed/Setup/Build UnityChan Player Prefab` を
> 実行してください（URP マテリアル変換＋クリップ複製）。モデルが無い環境ではプリミティブへ
> 自動フォールバックします。

## 4. コードで使う

### 最小例 — 2 クリップだけの RiggedAvatar

```csharp
using Seed.Character;
using Seed.Motion;
using UnityEngine;

// モデルの GameObject（Animator つき。Controller アセットは未設定でよい）
var avatar = model.AddComponent<RiggedAvatar>();

// クリップ台帳: モーション追加 = Add 1行
var motions = new MotionSet()
    .Add(MotionClipId.Idle, idleClip)          // fade 0.15 秒 / 速度 1 / ループはクリップ準拠
    .Add(MotionClipId.Locomotion, runClip);

// 生成直後に一度だけ結線（呼ばないと遷移しても無反応）
avatar.Configure(model.GetComponent<Animator>(), motions);

// あとはキャラ基盤へ渡すだけ。Behavior 遷移が自動でクロスフェードになる
var agent = CharacterFactory.Create(
    new CharacterId(1),
    new CharacterDefinition(),
    new ActorBlueprint(new ActorKey(1), avatar));
```

BehaviorKey と MotionClipId は標準の番号が揃えてあり（Attack=3 → Motion#3 など）、既定の MotionBindings が「同値素通し」で解決するため対応表を書く必要はありません。

### 実戦例 — 実モデルへのフル装着（イベント窓＋上半身レイヤー＋リグ一式）

```csharp
using Seed.Character;
using Seed.Motion;
using UnityEngine;

// --- 1) 再生系: ‰イベント（当たり判定窓）と上半身マスクつき ---
var motions = new MotionSet()
    .Add(MotionClipId.Idle, idleClip)
    .Add(MotionClipId.Locomotion, runClip)
    .Add(MotionClipId.Attack, slashClip, fadeSeconds: 0.08f, speed: 1f, loop: false,
        new MotionEvent(300, AvatarEventId.HitboxBegin),   // クリップ 30% 地点で判定開始
        new MotionEvent(600, AvatarEventId.HitboxEnd));    // 60% 地点で終了（‰は昇順で登録＝同一Tickの発火順を明快にするため）

var avatar = model.AddComponent<RiggedAvatar>();
avatar.Configure(animator, motions,
    bindings: null,                 // null = 同値素通し。ズレる時だけ MotionBindings を渡す
    upperBodyMask: upperMask);      // AvatarMask（任意）: 走りながら上半身だけ攻撃、が可能になる

// --- 2) ボーン取得（Humanoid なら Animator から引ける） ---
var head     = animator.GetBoneTransform(HumanBodyBones.Head);
var shoulder = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
var elbow    = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
var hand     = animator.GetBoneTransform(HumanBodyBones.LeftHand);
var hips     = animator.GetBoneTransform(HumanBodyBones.Hips);

// --- 3) リグ一式（登録順 = 適用順。揺れものは必ず最後） ---
var look = new LookAtRig((head, 1f, 70f));                        // (ボーン, 配分, 最大角)の列
var arm  = new TwoBoneIkRig(shoulder, elbow, hand,
    poleHint: model.transform.position - model.transform.forward * 2f + Vector3.up);
var footSettings = new FootIkSettings                               // 既定値は人型向け
{
    MaxStepHeight = 0.45f,                                         // これを超える高低差は足場と見なさない
    MaxSlopeDegrees = 50f,                                         // これより急な面は壁として扱う
    MaxHipDrop = 0.35f,                                            // 腰を沈める上限
};
var foot = new FootIkRig(hips,
    new FootIkRig.Leg(                                             // 第4引数のつま先は任意
        animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg),
        animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg),
        animator.GetBoneTransform(HumanBodyBones.LeftFoot),
        animator.GetBoneTransform(HumanBodyBones.LeftToes)),
    new FootIkRig.Leg(
        animator.GetBoneTransform(HumanBodyBones.RightUpperLeg),
        animator.GetBoneTransform(HumanBodyBones.RightLowerLeg),
        animator.GetBoneTransform(HumanBodyBones.RightFoot),
        animator.GetBoneTransform(HumanBodyBones.RightToes)),
    groundMask, footSettings, castRange: 1f);                      // 地面問い合わせを渡す形もある

var hairParams = SpringBoneParams.Default;                         // 髪向け既定: 40/0.2/(0,-2,0)/0.05
hairParams.Stiffness = 25f;                                        // 柔らかめに調整
var hair = new SpringBoneRig(hairBones,                            // 根本→先端の Transform[]（2本以上）
    hairParams,
    colliders: new[] { (head, 0.12f) });                           // 頭への めり込み防止球

avatar.gameObject.AddComponent<MotionRig>()
    .With(look).With(arm).With(foot).With(hair);                   // 登録順に LateUpdate で適用

// --- 4) 目標の更新は毎フレーム App が行う（重み・生死の方針は App の仕事） ---
look.SetTarget(enemyPosition + Vector3.up * 0.5f);                 // 注視先
arm.Weight = 1f - distance / 3f;                                   // 距離フェードは呼び出し側で
arm.SetTarget(enemyPosition + Vector3.up * 0.6f);
// 使わない時: look.ClearTarget(); arm.ClearTarget();
```

イベントは `IAvatarEventSink` 経由で現在の Behavior へ届きます（CharacterFactory 経由なら結線は自動）。クリップ側に AnimationEvent を仕込む流儀でも、関数名 `OnAnimationEvent`・int 引数（AvatarEventId 値）で同じ経路に乗ります。

> 📖 **用語 — AvatarMask**: ボーンの部分集合を定義する Unity アセット。AnimationDriver の上半身レイヤーに渡すと、そのボーンだけ上半身クリップで上書きされます（下半身は全身レイヤーの走りのまま）。

> 📖 **用語 — 2ボーンIK / ポールヒント**: IK（Inverse Kinematics）は「先端を目標へ届かせる関節角を逆算する」計算。肩-肘-手のような 2 関節は余弦定理で解析的に解けます（TwoBoneIkSolver）。ポールヒントは肘・膝を「どちら側へ曲げるか」を決めるワールド空間の目印です。

## 5. 仕組み

### Playables グラフの構成

`AnimationDriver.Initialize` は `PlayableGraph` を作り、`AnimationPlayableOutput → AnimationLayerMixerPlayable（0=全身 / 1=上半身）→ 各層 2 スロットの AnimationMixerPlayable` を接続します。再生（`Play` / `PlayUpper`）は空いている側のスロットへ新クリップを差してフェードを開始する「ピンポン」方式で、フェード完了（重み 1.0 到達）後に旧スロットを破棄します。

グラフは `DirectorUpdateMode.Manual` で、`RiggedAvatar.Update` が `Driver.Tick(Time.deltaTime)` を呼んで初めて進みます——規約「状態は Tick、艶は Update」のとおり、アニメ再生は Update 側（艶）の仕事です。

フェード重みの計算は純C#の `CrossfadeState` が持ちます。線形フェードで現在モーションの重みを 0→1 に運び、Playables への適用（`SetInputWeight`）はバックエンドの `AnimationDriver` が行う分離です。純C#なのでフェードとイベントのタイミングは EditMode テストで数値検証できます。

### 正規化‰イベント（当たり判定窓）

> 📖 **用語 — 正規化‰（パーミル）**: クリップの再生位置を長さで割った正規化時間（0〜1）を 1000 倍した整数。`new MotionEvent(300, …)` は「クリップの 30% 地点」。秒ではなく比率なので、クリップを差し替えても速度を変えても相対位置が保たれます。

`CrossfadeState.Tick` は毎フレーム「前回の‰ → 今回の‰」の区間 (from, to] を調べ、区間に入ったイベントを登録順に発火します。ループモーションが折り返したフレームでは **末尾側（from→1000）→ 先頭側（0→to）** の順で発火し、取りこぼしません（0‰ちょうどのイベントも先頭側で拾われます）。

発火したイベント ID は `AnimationDriver.MotionEventFired`（`Action<int>`）→ `RiggedAvatar` → `IAvatarEventSink.PostAvatarEvent` の経路で現在の Behavior へ届きます。これは「演出の時間軸→論理の時間軸」の**唯一の逆流**であり、当たり判定の開始・終了・コンボ受付窓をアニメ側の真実で駆動するための穴です。なおイベントは**全身レイヤーのみ**発火します（上半身レイヤーは `fireEvents: false` に固定。イベントの真実を一本化するため）。

### 揺れものの固定刻みシミュレーション

> 📖 **用語 — Verlet 積分**: 速度変数を持たず「現在位置と前回位置の差」を速度として使う数値積分。位置を直接拘束（引き戻し）しても発散しにくく、髪・布のシミュレーション定番です。

`SpringBoneChain` は Transform に触れない純数学です。各関節は毎ステップ、

1. **慣性**: `(current - previous) * (1 - Drag)` で前ステップの速度を引き継ぎ
2. **復元**: アニメ素姿勢での親→子の向きへ `Stiffness * h` で引かれ
3. **重力・外力**: `(Gravity + ExternalForce) * h` を加算し
4. **長さ拘束**: 親からボーン長ちょうどの球面へ射影（伸び縮み禁止）
5. **押し出し**: 球コライダーへのめり込みを押し出し、長さ拘束と交互に最大 4 回反復

時間の進め方が要点です。`Step(deltaSeconds, …)` は渡された時間をそのまま使わず、内部のアキュムレータ（貯金）へ足し、**厳密に 1/60 秒の固定刻み**が貯まったぶんだけ前進させて端数を次回へ繰り越します。刻み幅が完全に一定なので Stiffness や Drag の効き方が FPS に依存せず、60fps でも 30fps でも同じ質感になります。貯金の上限は 0.1 秒——ヒッチ（巨大な dt）が来ても最大 6 ステップで打ち切られ、爆発しません。裏返しの仕様として、固定刻み未満の dt しか来ないフレームでは前進しないことがあります。

> 📖 **用語 — 素姿勢**: 本プロジェクトの揺れもの用語で、「アニメだけを適用した、揺れ加工前の関節位置」。SpringBoneRig は装着時の各ボーンのローカル回転を記憶し、毎フレーム **復元してから** 位置を採取します。復元しないと前フレームの自己出力（揺れ加工後の回転）を復元目標として読んでしまい、髪型が崩れて垂れ切るフィードバックが起きます。だから**揺れボーン列はアニメクリップで焼いてはいけません**。

`SpringBoneRig.Apply` はさらに、フレーム番号の飛び（無効化からの復帰）や根本の 2m 超の瞬間移動（既定 `teleportThreshold: 2f`）を検出すると `Reset` で即座にアニメ姿勢へ一致させます——ワープ後に髪が鞭のように吹き飛ぶのを防ぐ安全弁です。書き戻しは回転差分のみ（位置を直書きしない）なので、スキニングされたモデルでも破綻しません。

### 三大規約との関係

- **状態は Tick、艶は Update**: 行動・位置の真実は Behavior 状態機械と ActorPose（Tick 側）にあり、Seed.Motion は一切書き込みません。再生は Update、リグは LateUpdate ——すべて「見た目の補正」です。MotionRig が LateUpdate なのは、Animator/Playables のボーン書き込みが Update 後に確定し、その**上へ**重ねる必要があるからです。
- **方針は App**: 「誰を注視するか」「腕IKを何 m からフェードするか」「風をどれだけ当てるか」は演出の方針であり、デモでは `Sample_BattlePhase.UpdateDemoRig` が毎フレーム決めています。リグは実行部に徹します。
- **命令の処理者は1基盤**: クリップ再生の命令を処理するのは AnimationDriver ただ一つです。AnimatorController という第二の状態機械を持ち込まないことで、「なぜこのモーションが流れたか」の答えが常に Behavior 遷移に一本化されます。

> 📖 **用語 — ルートモーション**: 移動量がアニメクリップ自体に焼き込まれている方式（踏み込みながら斬る、など）。Animator が Transform を直接動かすと「状態は Tick」が壊れるため、`RootMotionRelay` が `OnAnimatorMove` で移動量を**貯めるだけ**にし、Tick 側が `TakeDelta()` / `TakeRotation()` で取り出して MotionSolver / Pose へ流します。壁判定・リプレイと矛盾しません。

## 6. よくあるつまずき

- **症状**: 行動が遷移してもモデルが無反応 → **原因**: `Configure` 未呼び出し（OnBehaviorChanged が何もしない） → **対処**: `AddComponent<RiggedAvatar>()` の直後に一度だけ `Configure(animator, motionSet)` を呼ぶ
- **症状**: 特定のモーションだけ再生されない → **原因**: MotionSet 未登録の MotionClipId は**静かに何もしない**設計（例外にせずプリミティブ演出へ委ねる） → **対処**: `MotionSet.Add` の登録漏れ、または番号ズレ（`MotionBindings.Bind` が必要か）を確認
- **症状**: 当たり判定イベントが発火しない → **原因**: 上半身レイヤーで再生している（イベントは全身レイヤーのみ発火）、`MotionSet.Add` の `events` 引数に渡し忘れている、または `MotionSet` 自体が未登録 → **対処**: 判定つきモーションは `Play`（全身）で流し、`events` の登録を確認する
- **症状**: 同一 Tick に入った複数イベントの発火順が意図と違う → **原因**: `MotionEvent` は配列順に発火する（‰順ではない） → **対処**: `MotionSet.Add` へ渡す `MotionEvent` を‰昇順で並べる（並び順が発火順。昇順でなくても発火自体は起きます）
- **症状**: 髪が垂れ切る・髪型が崩れていく → **原因**: 揺れボーン列をアニメクリップで焼いている（素姿勢の復元と喧嘩する） → **対処**: 揺れボーンをクリップのカーブから外す。素姿勢は装着時のローカル回転で定義される
- **症状**: `ArgumentException: 揺れものリグには2本以上のボーン列が必要です。` → **原因**: SpringBoneRig にボーン 1 本だけ渡した → **対処**: 根本→先端の 2 本以上の列を渡す（1 本では節が作れない）
- **症状**: Weight=0 にしたのに揺れものの計算が走っている → **原因**: 仕様。IK 系の Weight=0 は「何もしない」だが、SpringBoneRig は**シミュレーションだけ進めて書き戻しを止める**（再開時に不連続にならないため） → **対処**: そのままでよい。負荷が問題なら MotionRig への登録自体を外す
- **症状**: ルートモーション付き攻撃で壁を抜ける・リプレイがズレる → **原因**: Animator の移動量を Transform へ直接適用している → **対処**: `RootMotionRelay` を付け、Tick 側で `TakeDelta()` を消費して MotionSolver / Pose へ流す
- **症状**: ポニーテールが注視に追従しない → **原因**: MotionRig の登録順が違う（登録順＝適用順） → **対処**: SpringBoneRig を注視・IK の**後**に `With` する

## 7. 増やす・拡張する

- **モーションを増やす**: `MotionSet.Add(new MotionClipId(100), clip, …)` の 1 行（アプリ独自は 100 以降推奨）。BehaviorKey と番号を揃えれば対応表不要、ズレる場合のみ `new MotionBindings().Bind(behaviorKey, clipId)` を Configure に渡す
- **イベントを増やす**: `MotionSet.Add` の events 引数に `new MotionEvent(‰, id)` を追加（クリップ非編集）。ID は AvatarEventId の標準 6 種（HitboxBegin=1 〜 Footstep=6）かアプリ独自 100 以降
- **上半身演出**: `avatar.Driver.PlayUpper(clipId, weight, fadeSecondsOverride)` / `ClearUpper()` を直接叩く。範囲は Configure の第 4 引数（AvatarMask）で指定
- **独自リグ**: `IPoseRig`（`float Weight { get; set; }` ＋ `void Apply()` の 2 メンバ）を実装し `MotionRig.With` で登録。Apply は LateUpdate タイミングで呼ばれる。既製リグ（LookAtRig 約 90 行）が実装の手本
- **多関節の連結**: 尻尾・触手・鎖は `new ChainIkRig(joints, iterations: 8)`（FABRIK）
  > 📖 **用語 — FABRIK**: Forward And Backward Reaching IK。関節列を先端側から・根本側から交互に引き直して収束させる反復法。関節数が多くても安定
- **揺れの質感**: `SpringBoneParams` の 4 値（Stiffness=硬さ / Drag=減衰 0〜1 / Gravity=垂れ / JointRadius=関節の太さ）。既定は髪向け 40 / 0.2 / (0,−2,0) / 0.05。マントは Gravity 強め、アホ毛は弱め
- **風の演出**: `SpringBoneRig.ExternalForce` へ毎フレーム外力を設定（風向・強弱の方針は App が持つ）
### 足の接地適応（階段・段差・坂）

アニメーションクリップは平地を前提に作られているため、階段や坂ではそのままだと足が浮く/めり込みます。
`FootIkRig` はアニメの上に「実際の地面へ合わせる補正」を重ねます。解き方は5段階です。

1. **必要な上下量を測る**。各足について「アニメ位置から地面（＋足首の高さ）へ合わせるのに必要な量」を
   キャラの上方向へ射影して求めます
2. **足場かどうか判定する**。その量が `MaxStepHeight` を超える足は「そこは足を置く場所ではない」と
   判断して適用率を 0 へ落とします——階段を上るとき、**1段先の蹴上げに足が吸い付いて脚が伸び切るのを防ぐ**
   のがこの判定です（穴の縁でも同様に働きます）
3. **深い側に合わせて腰を沈める**。両足のうち低い地面に合わせる必要がある側に合わせ、`MaxHipDrop` で
   クランプします。片足だけ届かず伸び切る不自然さを避ける、アクションゲームの定番の作りです
4. **足裏を法線へ沿わせる**。アニメの回転を保ったまま足の上方向だけを地面の法線へ倒します。
   ただし `MaxSlopeDegrees` を超える面は壁とみなし、**回転合わせをしません**（壁に足裏を貼らない）
5. **時間追従で平滑化する**。すべての量を速度制限つきで追従させます。段差をまたぐ瞬間に足が飛ばないため。
   初回だけは平滑化せず即座に合わせます（出現フレームのガクつきを避ける）

設定は `FootIkSettings`（既定値は人型・身長 1.7m 前後を想定）。

| 項目 | 既定 | 意味 |
|---|---|---|
| `FootHeight` | 0.1 | 接地点から足ボーンまでの高さ（m） |
| `MaxStepHeight` | 0.45 | 足を合わせる高低差の上限（m）。超えたら IK を切る |
| `MaxSlopeDegrees` | 50 | 足裏を沿わせる傾斜の上限（度）。超えたら壁として扱う |
| `MaxHipDrop` | 0.35 | 腰を沈められる上限（m）。座り込みを防ぐ |
| `FootFollowSpeed` | 3.5 | 足の追従速度（m/s）。大きいほど地形に忠実 |
| `HipFollowSpeed` | 1.8 | 腰の追従速度（m/s）。足より遅くすると重心移動が滑らかに見える |
| `FootRotationSpeed` | 360 | 足の回転追従（度/s） |
| `WeightFadeSpeed` | 6 | 接地重みのフェード速度（1/s）。空中への切り替えを滑らかにする |
| `RotateToNormal` | true | 法線へ沿わせるか（false なら位置合わせのみ） |

> 📖 **用語 — つま先レイ**: `FootIkRig.Leg` の第4引数につま先ボーンを渡すと、
> かかととつま先の**2点**で地面を探し、**高い方**を採用します。段差の縁に立ったとき、
> 低い側に合わせて足が地面へ埋まるのを防ぐためです。

**地面の問い合わせは `IGroundProbe` で抽象化**しています（既定の実装は `PhysicsGroundProbe`＝
`Physics.Raycast`）。理由は2つあり、1つは EditMode テストで階段・傾斜・穴を偽の地面として与えて
接地の解き方を検証できること（`FootIk_` で始まるテスト9件がそれです）、
もう1つはハイトマップ・ボクセル・NavMesh を地面にするゲームでも本体を書き換えずに済むことです。

キャラ本体の高さ（どの段に立っているか）は足IKの担当ではなく `IMotionSolver` の担当です。
デモは App 実装の `Sample_GroundSnapSolver` が本体の高さを地面へ吸着させ、
**足元の細かな凹凸は足IKが吸収する**二段構成にしています。

## 8. 関連ファイルとテスト

- `Assets/Script/Motion/Runtime/RiggedAvatar.cs` — IAvatar 実装＋MotionBindings
- `Assets/Script/Motion/Runtime/AnimationDriver.cs` — Playables グラフと 2 レイヤー再生
- `Assets/Script/Motion/Runtime/MotionSet.cs` / `MotionClipId.cs` — クリップ台帳と論理名
- `Assets/Script/Motion/Runtime/CrossfadeState.cs` — MotionDescriptor / MotionEvent / フェード状態機械
- `Assets/Script/Motion/Runtime/RootMotionRelay.cs` — ルートモーション中継
- `Assets/Script/Motion/Runtime/Rig/` — MotionRig / LookAtRig / TwoBoneIkRig / ChainIkRig / FootIkRig / SpringBoneRig
- `Assets/Script/Motion/Runtime/Ik/` — LookAtSolver / TwoBoneIkSolver / FabrikSolver（純数学）
- `Assets/Script/Motion/Runtime/Ground/` — IGroundProbe / GroundHit / PhysicsGroundProbe / FootPlacementSolver / FootIkSettings
- `Assets/Script/App/Samples/Sample_GroundSnapSolver.cs` — 本体の高さを地面へ吸着（App の方針実装）
- `Assets/Script/Motion/Runtime/Spring/SpringBoneChain.cs` — 揺れシミュレーション本体
- `Assets/Script/App/Samples/Sample_BattlePhase.cs` — デモ装着（AttachDemoMotionRig / UpdateDemoRig）
- テスト: `Assets/Script/Motion/Tests/Editor/MotionTests.cs` — IK 解析解・‰イベント（ループ折り返し含む）・揺れの数値検証。ソルバーと CrossfadeState が純C#だから EditMode で検証できます

[← 前: 08_Character](08_Character.md) | [索引](README.md) | [次: 10_AI →](10_AI.md)
