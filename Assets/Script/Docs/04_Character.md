# 04. Seed.Character — アクター制御（Agent / Actor / Behavior / Avatar）

1キャラ = `CharacterAgent`（純C#）。Agent が複数の表現形態（`CharacterActor`）を束ね、
Actor が Behavior 状態機械・姿勢（ActorPose）・表示物（IAvatar）を束ねる。
真実は純C#側にあり、表示は写しに徹する（規約「状態は Tick、艶は Update」）。

## デモで試す

統合デモ（[01_Demo.md](01_Demo.md)）の戦闘フェーズがこの基盤の実演:

- WASD 移動・[1]攻撃・[G]ガード（シアン着色）——ManualLogic → Behavior 遷移
- **[T] で 3D カプセル ⇔ 2D 立ち絵の Actor 切替**（位置・向きは引き継ぎ、行動は Idle から再出発）
- 攻撃=踏み込み / 被弾=赤フラッシュ+シェイク / 死亡=0.5秒で倒れる（Avatar3D の簡易演出）

## 最小コード（表示なし・MonoBehaviour 不要で動く）

```csharp
using Seed.Character;
using Seed.Hub.Contracts;
using UnityEngine;

var registry = new CharacterRegistry();
var characters = new CharactersManager(registry);
var players = new PlayersManager(registry);
characters.AddManager(players);

var agent = CharacterFactory.Create(
    new CharacterId(1),
    new CharacterDefinition { MoveSpeed = 4f },   // 標準行動6種が自動装着
    new ActorBlueprint(new ActorKey(1), NullAvatar.Instance));

var manual = new ManualLogic();
players.Add(new PlayerController(agent, manual));

// 毎フレーム:
manual.SetMove(new Vector3(0f, 0f, 1f));   // 前進の意図
manual.RequestAction(BehaviorKey.Attack);  // 離散行動を1回予約
characters.Tick(Time.deltaTime);           // 陣営を登録順に決定的駆動
// → agent.ActiveActor.Pose.Position が前進し、CurrentKey が Attack→Idle と遷移
```

Hub 接続（被弾リアクションの采配・ICharacterQuery の貸し出し）は
`CharacterSystem.Initialize(hub, services, characters)` を合成ルートで1回。

## 部品の対応表

| 部品 | 役割 | 実装の入口 |
|---|---|---|
| CharacterAgent | キャラ1ユニット（Actor 切替・リアクション窓口） | `CharacterFactory.Create` |
| CharacterActor | 1表現形態（Behavior + Pose + Avatar） | `ActorBlueprint` |
| ICharacterBehavior | 行動1つ（Idle/Attack/…） | `CharacterDefinition.WithBehavior` |
| IAvatar | 表示物（3D/2D/リグ付き/なし） | `Avatar3D` / `Avatar2D` / `RiggedAvatar` / `NullAvatar` |
| ManualLogic / AiBrain | 意図の供給源（人間/AI 共通の語彙） | `PlayerController` / `EnemyController` |
| CharactersManager | 全陣営の決定的 Tick と2フェーズリアクション | 合成ルートで new |

## ハマりどころ

- `CharacterActor` の avatar は null 禁止（表示なしなら `NullAvatar.Instance` を明示）
- 未登録 BehaviorKey への RequestBehavior は HubException（打ち間違いを即検出）
- UnitManager の Tick 中の Add/Remove は HubException（順序決定性の保護。次フレームで）
- 被弾・死亡リアクションは Hub 経由なら「Tick 中は保留・Tick 後一括適用」が保証される。
  `PostReaction` 直呼びはテスト・演出スクリプト用
- 物理と統合するなら `CharacterControllerMotionSolver` を `actor.MotionSolver` へ
  （生成ステージの壁で遮られるのはこれ。既定は素通しの DirectMotionSolver）

## 増やすとき

- 独自行動: `ICharacterBehavior` 実装（or `TimedBehaviorBase` 派生）→
  `CharacterDefinition.WithBehavior(() => new MyBehavior())`。キーは BehaviorKey(100以降)
- 陣営追加（中立NPC等）: UnitManager 派生 → `CharactersManager.AddManager` 1行
- 新表示形態: `IAvatar` 実装（6メソッド）

主要ファイル: `Assets/Script/Character/Runtime/Agent/CharacterAgent.cs` /
`Manage/CharacterFactory.cs` / `Manage/CharactersManager.cs`。
テスト: `Assets/Script/Character/Tests/Editor/`
