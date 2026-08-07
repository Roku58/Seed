# 08. Seed.Character — アクター制御 — 真実は純C#、表示は写し

[← 前: 07_Data](07_Data.md) | [索引](README.md) | [次: 09_Motion →](09_Motion.md)

## この章で分かること

- キャラ1体がどんな部品（Agent / Actor / Behavior / Avatar / Logic / Controller / Manager の7語）で出来ているか
- 行動（Behavior）状態機械の遷移裁定——「攻撃中はガード不可」「被弾は攻撃を割り込む」が優先度の数値だけで成立する仕組み
- 被弾・死亡リアクションの2フェーズ適用（Tick 中は保留・Tick 後に一括）がなぜ必要か
- MonoBehaviour なしでキャラを生成して動かす最小コード
- 独自行動（回避など）を基盤に手を入れず追加する手順

## 前提

- [02_Hub](02_Hub.md): `MessageHub` の命令（`SubscribeCommand<T>`）、`ServiceRegistry`、`HubException`、合成ルートの考え方
- [03_Clock](03_Clock.md): 「毎フレーム dt を渡して Tick する」駆動の形
- [01_Demo](01_Demo.md): 統合デモの起動方法（この章の「動かして試す」はその戦闘フェーズを使う）

この章が初出の主な用語: Agent、Actor、Behavior（行動）、Avatar、Logic（意図）、Controller、Manager（陣営）、リアクション、2フェーズ適用、`BehaviorKey`、`ActorPose`、`IMotionSolver`

> 📖 **用語 — 純C#**: 本プロジェクトでは「MonoBehaviour や GameObject に依存しない普通の C# クラス」を指します。シーンに置かなくても `new` で作れて、Unity を起動しないエディタテストでそのまま検証できます。

> 📖 **用語 — 状態機械（ステートマシン）**: 「今の状態は常に1つ」「決められた規則でだけ次の状態へ移る」仕組み。この章では「キャラの現在の行動は常に1つ（Idle か Attack か…）」を保証する装置として登場します。

## 1. これは何か

Seed.Character は「キャラクター1体を、表示（GameObject）と切り離した純C#の状態機械として駆動する」基盤です。位置・向き・現在の行動という真実はすべて純C#側が持ち、3Dモデルや2D立ち絵はその写しを表示するだけです（規約「状態は Tick、艶は Update」）。

これが無いと次に困ります。

- 行動制御を MonoBehaviour に書くと、Update の呼ばれ順が Unity 任せになり「誰が先に動いたか」が実行ごとに揺れます（リプレイ・テストが壊れる）
- 「攻撃中にガードを押したら？」「のけぞり中に再被弾したら？」の割り込み規則を if 文の山で書くと、行動が増えるたびに全組み合わせを直すことになります
- 表示物の破棄を各所が勝手にやると、退場時に GameObject がリークします

Seed.Character はこの3つを「Add 順の決定的 Tick」「優先度による一元裁定」「`UnitManager.Remove` → `Avatar.Release` の一気通貫」で構造的に解決します。HP やダメージ計算はここには**ありません**——それはロジック側（[12_GameCore](12_GameCore.md)）の真実で、この基盤は通知を受けて演じるだけです。

## 2. 全体像

### 7語の関係図（これだけ覚えれば読める）

```
CharactersManager（全体管理: 陣営を登録順に Tick ＋ リアクションの2フェーズ采配）
 └─ UnitManager（陣営 Manager: PlayersManager / EnemiesManager。ユニットを Add 順に Tick）
     └─ UnitController（結線: PlayerController / EnemyController。Logic と Agent を繋ぐ）
         ├─ ICharacterLogic（頭脳 = Logic: ManualLogic（人間）/ AiBrain（AI、10章））
         │      └─ 毎Tick「意図（CharacterIntent）」を作る
         └─ CharacterAgent（キャラ1ユニット = Agent。Actor の束＋リアクション窓口）
             └─ CharacterActor（1表現形態 = Actor。表示中は常に1体だけ）
                 ├─ ICharacterBehavior × N（行動 = Behavior。Idle/Attack/… の状態機械）
                 ├─ ActorPose（位置・向き・水平速度の写し）
                 └─ IAvatar（表示物 = Avatar: Avatar3D / Avatar2D / RiggedAvatar / NullAvatar）
```

- **Agent** はキャラの戸籍。3Dモデル用と2D立ち絵用など複数の **Actor** を束ね、表示に立てるのは常に1体です
- **Actor** は「Behavior 状態機械＋姿勢＋表示物」のセット。行動の裁定はすべてここで行われます
- **Logic** は入力や AI から「こう動きたい」という意図を作るだけで、キャラを直接は動かしません
- **Controller** は Logic と Agent を毎 Tick 繋ぐ配線、**Manager** はそれを決定的な順序で駆動する台です

### 部品表

| 部品 | 実体 | 役割 |
|---|---|---|
| `CharacterAgent` | 純C# | キャラ1ユニット。Actor 切替・リアクション受け口 |
| `CharacterActor` | 純C# | 1表現形態。遷移裁定・`ActorPose`・`IAvatar` の束 |
| `ICharacterBehavior` | 純C# | 行動1つ（標準6種＋アプリ独自） |
| `IAvatar` | 契約 | 表示物（`Avatar3D` / `Avatar2D` / `RiggedAvatar` / `NullAvatar.Instance`） |
| `ManualLogic` / `AiBrain` | 純C# | 意図の供給源（人間 / AI。語彙は共通） |
| `PlayerController` / `EnemyController` | 純C# | Logic と Agent の結線 |
| `PlayersManager` / `EnemiesManager` | 純C# | 陣営（`UnitManager` 派生）。Add 順 Tick |
| `CharactersManager` | 純C# | 全陣営の駆動入口＋リアクション采配 |
| `CharacterRegistry` | 純C# | 在籍名簿。`ICharacterQuery` / `ICharacterRoster` の実体 |
| `CharacterFactory` / `CharacterDefinition` | 純C# | レシピからのユニット組み立て |
| `CharacterSystem` | 純C# | Hub 接続点（購読と窓口貸し出し） |

### データの流れ（1フレーム）

```
入力/AI ──▶ Logic ──▶ Controller ──▶ Agent ──▶ Actor ──▶ Avatar
 (App)    意図を保管   LogicFrame を   意図を    ①意図反映     ApplyPose で
          ・変換       組んで Think    転送     ②遷移解決     Transform へ写す
                                               ③行動 Tick
                                               ④姿勢反映
逆流は1本だけ: Avatar のアニメイベント ──▶ IAvatarEventSink（実装は Actor）──▶ 現在の Behavior
```

## 3. 動かして試す

統合デモ（[01_Demo](01_Demo.md)）の戦闘フェーズがこの基盤の実演です。

1. Unity で新規シーンを作成します（専用シーンはありません。カメラ・ライトは無ければ自動生成されます）
2. Hierarchy で右クリック → Create Empty で空の GameObject を作成
3. Inspector の Add Component で `Sample_GameFlowRunner` を追加
4. Play を押す → ホーム画面（テキストパネル）が表示されます
5. **[1]** キーで「出撃: 草原（敵の攻撃: ゆっくり）」（=Stage1）→ 戦闘フェーズへ。Hierarchy にプレイヤー（青カプセル）と敵（灰キューブ）が現れます
6. **[W][A][S][D]** で移動——`ManualLogic.SetMove` → `LocomotionBehavior` が姿勢を進めています
7. **[1]** で攻撃——前方へ約0.3秒踏み込む簡易演出（`Avatar3D`）。連打しても2発目がすぐ出ないのは正常です（後述）
8. **[G]** を押している間ガード——カプセルがシアンに着色されます
9. **[T]** で Actor 切替——3Dカプセル（`ActorKey(1)`）⇔ 2D立ち絵（`ActorKey(2)`）。位置・向きは引き継がれ、行動は Idle から再出発します
10. 敵の攻撃を受けると赤フラッシュ＋横シェイク（被弾）、HP が尽きると0.5秒で倒れます（死亡）
11. **[B]** でホームへ帰還。Hierarchy からキャラの GameObject が消えます（`Remove` → `Avatar.Release` の一気通貫）

Console に特別なログは出ません。例外（`HubException`）が出るのは構成ミスのときだけです。

## 4. コードで使う

### 最小例（表示なし・MonoBehaviour 不要で動く）

```csharp
using Seed.Character;
using Seed.Hub.Contracts;
using UnityEngine;

// --- 合成ルートで1回 ---
var registry = new CharacterRegistry();              // 在籍名簿
var characters = new CharactersManager(registry);    // 全体管理
var players = new PlayersManager(registry);          // プレイヤー陣営
characters.AddManager(players);                      // 登録順 = Tick 順

var agent = CharacterFactory.Create(
    new CharacterId(1),
    new CharacterDefinition { MoveSpeed = 4f },      // 標準行動6種が自動装着
    new ActorBlueprint(new ActorKey(1), NullAvatar.Instance)); // 表示なしは明示

var manual = new ManualLogic();                      // 入力の保管庫
players.Add(new PlayerController(agent, manual));    // Logic と Agent を結線

// --- 毎フレーム ---
manual.SetMove(new Vector3(0f, 0f, 1f));   // 前進の意図（毎フレーム上書き）
characters.Tick(Time.deltaTime);           // 全陣営を決定的に駆動
// → CurrentKey が Locomotion になり agent.ActiveActor.Pose.Position が前進する

// --- 攻撃したいフレームだけ（1回ぶん予約・後勝ち） ---
manual.RequestAction(BehaviorKey.Attack);
// → CurrentKey が Attack（0.4秒の拘束。位置を動かすのは Locomotion だけなのでこの間は前進しない）
//    → 完了後は移動意図が残っているので Locomotion へ戻り、再び前進する
//    （SetMove を呼んでいなければ Idle へ戻る）
```

Hub 接続（被弾リアクションの受信・`ICharacterQuery` の貸し出し）は合成ルートで1回、
`CharacterSystem.Initialize(hub, services, characters)` を呼ぶだけです。

> 📖 **用語 — 合成ルート**: アプリ起動時に「誰と誰を繋ぐか」を1か所で組み立てる場所（本プロジェクトでは `Sample_GameFlowRunner` などの App 層）。基盤同士はお互いを知らず、ここでだけ結線されます。

### 実戦例: 独自行動「回避」を自作する

「0.3秒だけ拘束され、向いている方向へ素早く滑る。攻撃はキャンセルできるが、被弾には割り込まれる」回避を追加します。基盤コードは一切変更しません。

```csharp
using Seed.Character;
using UnityEngine;

/// <summary>回避（ステップ）。0.3秒拘束し、正面方向へ素早く滑る。</summary>
public sealed class DodgeBehavior : TimedBehaviorBase
{
    /// <summary>アプリ独自キー（標準の1〜99を避けて100以降）。</summary>
    public static readonly BehaviorKey DodgeKey = new BehaviorKey(100);

    /// <summary>ステップ速度（m/s）。</summary>
    private const float DashSpeed = 10f;

    /// <summary>DodgeBehavior を生成する（拘束0.3秒）。</summary>
    public DodgeBehavior() : base(0.3f) { }

    /// <summary>この行動のキー。</summary>
    public override BehaviorKey Key => DodgeKey;

    /// <summary>優先度25 = 攻撃(20)を割り込めるが、被弾(30)には割り込まれる。</summary>
    public override int Priority => 25;

    /// <summary>正面方向へ滑る（時間管理は基底の base.Tick が担う）。</summary>
    public override void Tick(BehaviorContext context, float deltaTime)
    {
        base.Tick(context, deltaTime);                          // 経過秒を進める（拘束の管理）
        var forward = context.Pose.Rotation * Vector3.forward;  // 今向いている方向
        var delta = forward * (DashSpeed * deltaTime);
        // 位置更新は MotionSolver 経由（物理統合時も本クラス無変更で壁ずりが効く）
        context.Pose.Position = context.MotionSolver.Move(context.Pose.Position, delta);
    }
}
```

装着と発動は2行ずつです。

```csharp
// 装着: レシピに生成器（Func<ICharacterBehavior>）を足す。Actor ごとに専用インスタンスが作られる
var definition = new CharacterDefinition { MoveSpeed = 4f }
    .WithBehavior(() => new DodgeBehavior());
var agent = CharacterFactory.Create(new CharacterId(1), definition,
    new ActorBlueprint(new ActorKey(1), NullAvatar.Instance));

// 発動: 入力ハンドラから予約するだけ（AI からも同じ語彙で要求できる）
manual.RequestAction(DodgeBehavior.DodgeKey);
characters.Tick(deltaTime);   // → CurrentKey が Behavior#100 になり、0.3秒滑って Idle へ戻る
```

完了後に自動で Idle（や移動）へ戻るのは基底 `CharacterBehaviorBase.DesiredTransition` の既定挙動（`IntentProposal` の共通規則）で、自前で書く必要はありません。

> 📖 **用語 — `Func<ICharacterBehavior>`**: 「呼ぶと `ICharacterBehavior` を返す関数」を値として持てる C# のデリゲート型。Behavior は Actor ごとに専用インスタンスが要るため、`CharacterDefinition` は実体ではなく「生成器」で持ちます。

## 5. 仕組み

### Tick の決定的パイプライン

`CharactersManager.Tick(dt)` 1回で、次が常に同じ順序で起きます。

1. 陣営 Manager を**登録順**に Tick（例: プレイヤー陣営 → 敵陣営）
2. 各陣営はユニットを **Add 順**に Tick
3. 各ユニット（`UnitController.Tick`）: 姿勢から `LogicFrame` を組む → `Logic.Think` で意図を得る → `Agent.SetIntent` → `Agent.Tick`（死亡中は Think を飛ばし空の意図）
4. 表示中 Actor の Tick: ①意図の反映 → ②遷移解決 → ③現行動の Tick → ④`Avatar.ApplyPose` で姿勢を写す

> 📖 **用語 — 決定性**: 同じ入力列を与えたら毎回同じ結果になる性質。Unity の Update 順に頼らず「登録順・Add 順」という自前の順序で駆動することで、テスト・リプレイ・不具合再現が成立します。Tick 中の Add/Remove を例外で拒否するのもこの保護です。

### 遷移裁定——各 Behavior は「提案」しかしない

行動の切り替え規則は `CharacterActor.TryTransition` の1か所に集約されています。

- 同一キーへの再入は、その行動が `AllowsRefresh = true` のときだけ「頭からやり直し」を許す（既定 false。攻撃連打が毎回キャンセルで振り直される事故を防ぐ）
- `force` 指定（死亡・Actor 切替）は以降の審査をすべて飛ばして遷移する
- 現在行動の**実効優先度** = `IsCompleted ? int.MinValue : Priority`。つまり完了済みの行動は誰にでも席を譲る
- 次行動の `Priority` が実効優先度**以上**で、かつ `CanEnter` が真なら、`Exit` → `Enter` → `Transitioned` 通知 → `Avatar.OnBehaviorChanged` の順で遷移する

標準の優先度（`BehaviorPriorities`）は間を空けて定義されています。

| 行動 | Priority | 性質 |
|---|---|---|
| Idle / Locomotion | 0 | 常に完了済み。誰にでも譲る |
| Guard | 10 | `GuardHeld` が続く限り未完了 |
| Attack | 20 | 拘束0.4秒（既定）。未完了中の同キー再入は拒否 |
| （回避などアプリ独自） | 25 など | 隙間に差し込める |
| Hit | 30 | 拘束0.25秒（既定）。`AllowsRefresh = true`（多段ヒット） |
| Death | 100 | 決して完了しない終端。抜けるのは force のみ |

「攻撃中はガード不可（10 < 20）」「被弾は攻撃を割り込む（30 ≥ 20）」「死亡は何にも割り込まれない」が、if 文の組み合わせではなく数値の大小だけで成立します。

各 Behavior は毎 Tick `DesiredTransition` で「次に行きたい行動」を**提案**するだけで、実行するかは Actor が上の規則で裁定します。提案の既定は `IntentProposal.Next`——**ガード継続 > 離散行動の要求 > 移動 > なし**の順で意図を読みます（ガード中の攻撃キーが無視されるのはこの順序のため）。提案が `None` でも、完了済みの行動を放置しない保険として Actor が Idle への遷移を補います。

### 再入の保護——通知中の要求はキューへ

`Transitioned` 通知は同期イベントなので、「遷移通知 → Hub 発行 → 方針 → リアクション命令」の連鎖が**同じ Actor の遷移中にもう一度遷移を要求**することがあります。そのまま実行すると、内側の遷移が先に Avatar へ届いて表示と実状態がズレます。`CharacterActor` は `_isTransitioning` フラグでこれを検知し、通知中に届いた `RequestBehavior` を `PendingRequest` キューへ積んで、通知が終わってから届いた順に裁定します。

### 2フェーズリアクション——Tick 中は保留、Tick 後に一括適用

被弾・死亡などのリアクション命令は Hub の `PlayReactionCommand` として届き、`CharacterSystem`（処理者はこの1基盤だけ。規約「命令の処理者は1基盤」）が `CharactersManager.PostReaction` へ中継します。ここで2フェーズの采配が入ります。

- **Tick 実行中**に届いた命令は即時適用せず保留キューへ積む
- **全陣営の Tick 完了後**、届いた順に一括適用する（適用中に連鎖で増えた分も同じループで順に処理）

これが無いと「既に Tick 済みのユニットは今フレームの被弾を見ないが、未 Tick のユニットは見る」という順序非決定が生まれます。合成ルートの行儀に頼らず、基盤側で封じているのがポイントです。退場済みキャラへの命令は正常系として静かに無視されます（演出遅延中に死亡した場合など）。

適用の実体は `ReactionTranslator` で、`ReactionId` → 行動遷移の機械的な変換だけを行います（Hit → `BehaviorKey.Hit`、Death → 生存写しを落としてから force で `BehaviorKey.Death`、標準表に無い 100 以降の ID は**同じ値の `BehaviorKey` への遷移要求として素通し**）。「のけぞる**べきか**」（スーパーアーマー等）を決めるのはアプリの方針層です（規約「方針は App」）。

> 📖 **用語 — HubException**: 本プロジェクトが「構成ミス・規約違反」を即座に知らせるために投げる例外（[02_Hub](02_Hub.md)）。未登録キーの要求や Tick 中のユニット増減など、黙って動き続けると原因究明が難しくなるものは早期に落とします。

### 三大規約との関係

- **状態は Tick、艶は Update**: 位置・向き・現在行動の真実は `ActorPose` と Behavior が Tick で決め、`Avatar` は `ApplyPose` / `OnBehaviorChanged` で写すだけ。Avatar 側の Update に許されるのは色フェード等の純装飾です
- **方針は App**: 「攻撃開始を Hub の `AttackRequested` に変換するか」「被弾でのけぞらせるか」はアプリの方針。基盤は `BehaviorStarted` イベントと `ReactionTranslator` という機械的な口だけを提供します
- **命令の処理者は1基盤**: `PlayReactionCommand` を処理するのは `CharacterSystem` ただ1つ（`SubscribeCommand<PlayReactionCommand>`）

## 6. よくあるつまずき

- **症状**: `ArgumentNullException`（avatar）。**原因**: `CharacterActor` / `ActorBlueprint` に null の表示物を渡した。**対処**: 表示なしでも `NullAvatar.Instance` を明示的に渡します
- **症状**: `HubException`「〜は未登録の行動」。**原因**: `RequestBehavior`（や `RequestAction` 経由の要求）のキーが `AddBehavior` されていない。**対処**: `CharacterDefinition.WithBehavior` の登録漏れ・キー値の打ち間違いを確認。標準6種は `IncludeStandardBehaviors = true`（既定）で自動装着です
- **症状**: `HubException`「Tick 中のユニット増減は禁止」。**原因**: Behavior や Hub 購読の中から `UnitManager.Add` / `Remove` を呼んだ。**対処**: 生成・退場の要求はいったん自前のキューに積み、次フレーム（Tick 外）で実行します
- **症状**: `HubException`「表示中の Actor がいない」/「未起動（Activate 前に Tick）」。**原因**: `AddActor(activate: true)` の漏れや配線順ミス。**対処**: `CharacterFactory.Create` 経由なら最初の `ActorBlueprint` が自動で表示に立つため通常は踏みません
- **症状**: 攻撃ボタンを連打しても2発目がすぐ出ない。**原因**: `AttackBehavior` は未完了中の同キー再入を拒否します（`AllowsRefresh = false`）。**対処**: 正常仕様。連続攻撃はコンボ受付窓（`AvatarEventId.ComboWindowBegin/End`）を使った独自行動として設計します
- **症状**: ガード中に攻撃キーが効かない。**原因**: `IntentProposal` がガード継続を行動要求より先に見る仕様。**対処**: ガードを離してから攻撃します（仕様変更はアプリ側の独自 Logic で）
- **症状**: 被弾させたのに反映が1テンポ遅い気がする。**原因**: Hub 経由のリアクションは「Tick 中は保留・Tick 後一括適用」の2フェーズ。**対処**: 正常仕様（順序決定性の保護）。テスト・演出スクリプトで即時性が要る場合のみ `CharacterAgent.PostReaction` を直接呼びます（順序は自分で管理）
- **症状**: 生成ステージの壁をキャラがすり抜ける。**原因**: 既定の移動解決は素通しの `DirectMotionSolver`。**対処**: コライダー＋Rigidbody の移動モーター（サンプルの `Sample_KinematicMotor`＝自前 collide-and-slide）を `actor.MotionSolver` へ差します（[11_StageGen](11_StageGen.md)）
- **症状**: [T] で Actor を切り替えると行動がリセットされる。**原因**: 切替は「表現形態の交代」であり「行動の続行」ではない設計（位置・向きだけ `Pose.CopyFrom` で引き継ぐ）。**対処**: 仕様です

## 7. 増やす・拡張する

- **独自行動を足す**: ①`TimedBehaviorBase`（時間拘束型）か `CharacterBehaviorBase`（自由型）を継承したクラスを書く ②キーは `new BehaviorKey(100)` 以降 ③`CharacterDefinition.WithBehavior(() => new MyBehavior())` で装着 ④発動は `ManualLogic.RequestAction`（人間）/ AI の意図（[10_AI](10_AI.md)）/ Hub のリアクション素通し（下記）のいずれか。手順の実例は本章4節の `DodgeBehavior`
- **独自リアクションを足す**: `ReactionId(120)` のような 100 以降の ID は「同じ値の `BehaviorKey(120)` への遷移要求」として素通しされます。アプリは行動を装着しておくだけで、`ReactionTranslator` の変更は不要です
- **陣営を足す（中立NPCなど）**: `UnitManager` を派生し（コンストラクタで `CharacterRegistry` と `FactionId` を基底へ渡す）、合成ルートで `characters.AddManager(neutrals)` の1行。登録順が Tick 順になる点だけ意識します
- **新しい表示形態を足す**: `IAvatar` の6メソッド（`SetActive` / `ApplyPose` / `OnBehaviorChanged` / `SetLocomotionSpeed` / `BindEventSink` / `Release`）を実装します。最小リファレンスは `NullAvatar`、実モデル向けは `RiggedAvatar`（[09_Motion](09_Motion.md)）
- **物理・ナビメッシュと統合する**: `IMotionSolver`（現在位置と希望移動量から実際の到達位置を返す）を実装し `actor.MotionSolver` へ代入。null 代入で素通しに戻ります
- **プールで再利用する**: Agent を捨てずに `CharacterFactory.ResetForReuse(agent)` → 生存写しと全行動が初期化され Idle から再出発。スポーン位置は呼び出し側が `Pose.Position` へ入れ直します

## 8. 関連ファイルとテスト

- `Assets/Script/Character/Runtime/Agent/CharacterAgent.cs` — Actor の束・リアクション窓口・`BehaviorStarted`
- `Assets/Script/Character/Runtime/Agent/ReactionTranslator.cs` — `ReactionId` → 行動遷移の対応表
- `Assets/Script/Character/Runtime/Actor/CharacterActor.cs` — 遷移裁定・再入キュー・Tick 順序の本体
- `Assets/Script/Character/Runtime/Behavior/` — `ICharacterBehavior` / 基底2種 / 標準行動6種 / `BehaviorPriorities` / `IntentProposal`
- `Assets/Script/Character/Runtime/Core/` — `BehaviorKey` / `ActorKey` / `ActorPose` / `CharacterIntent` / `AvatarEventId` / `IMotionSolver`
- `Assets/Script/Character/Runtime/Logic/ManualLogic.cs` — 入力の一時保管 → 意図への変換
- `Assets/Script/Character/Runtime/Control/` — `UnitController` / `PlayerController` / `EnemyController`
- `Assets/Script/Character/Runtime/Manage/` — `CharacterFactory` / `CharacterDefinition` / `CharactersManager` / `UnitManager` / `CharacterRegistry` / `CharacterSystem`
- `Assets/Script/Character/Runtime/Avatar/` — `IAvatar` / `Avatar3D` / `Avatar2D` / `NullAvatar` / `CharacterControllerMotionSolver`
- テスト: `Assets/Script/Character/Tests/Editor/`（`BehaviorTransitionTests` = 裁定規則、`AgentReactionTests` = リアクション、`ActorSwitchTests` = 切替、`CharacterRegistryTests` / `CharacterSystemTests` / `ManualLogicTests`。`FakeAvatar` が表示なし検証の道具）

[← 前: 07_Data](07_Data.md) | [索引](README.md) | [次: 09_Motion →](09_Motion.md)
