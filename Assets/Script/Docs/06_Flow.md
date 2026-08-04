# 06. Seed.Flow — ゲームフェーズ遷移とステージ切替

ホーム・戦闘・ショップ等の遷移状態機械。`GameFlow` が `ChangePhaseCommand` の唯一の処理者。
遷移は「要求 → 次 Tick で OnExit → 非同期ロード待ち → OnEnter → PhaseChanged 発行」の一本道。
**同一フェーズへの再入も完全に Exit→Enter を回す**ため、ステージ切替は
「同じ戦闘フェーズへ別 payload（StageId）で再入」で表現する。

## デモで確認する

- ホーム [1] → 戦闘 → [B] → ホーム → [2] → **同じ戦闘フェーズに別ステージで再入**
  （地面の色・広さ・敵の攻撃間隔が差し替わる）
- Hierarchy でフェーズ入退場のたびルート GameObject が生成/破棄される
- [P] ポーズ中も [B] でホームへ戻れる（GameFlow は UnscaledDelta 駆動のため）

## 最小コード

```csharp
// 永続ルート（MonoBehaviour）の Start で:
var flow = new GameFlow(hub);          // ChangePhaseCommand の唯一の処理者になる
flow.AddPhase(new MyHomePhase(hub));   // GamePhase 派生（Id = new PhaseId(1)）
flow.AddPhase(new MyBattlePhase(hub));
flow.Start(new PhaseId(1));            // 入場は次の Tick

// 毎フレーム（Update）:
clock.Tick(Time.deltaTime);
flow.Tick(clock.UnscaledDelta);        // ポーズ中もメニュー遷移を効かせる

// どこからでも命令1発:
hub.PublishCommand(new ChangePhaseCommand(new PhaseId(2), payload: 201)); // ステージ201へ

// 終了時: flow.Dispose();  // 滞在フェーズの OnExit まで面倒を見る
```

フェーズ側は `GamePhase` を継承して `Id` / `OnEnter(payload)` / `OnExit` / `Tick(dt)` を書く。
シーンアセットを使うステージは `CreateLoadOperation(payload)` を override して
`ISceneLoader.LoadScene(...)` を返す（本番実装 `UnitySceneLoader`。ロード完了後に OnEnter）。

## ハマりどころ

- `Start`/`RequestChange` は予約のみ。実際の入場は次の Tick（退場フレームは誰も Tick されない）
- 未登録 PhaseId への遷移要求・AddPhase の二重登録は HubException
- 同一フェーズ再入でも状態は毎回消える。**持ち越したい状態（所持金・編成）はフェーズに
  置かず、永続ルート所有のサービス（ServiceRegistry 登録）へ**
- 遷移中に届いた要求は最新1件だけ保持（latest wins）
- ローディング画面は `flow.IsTransitioning` / `flow.LoadProgress` を UI が読む

## 増やすとき

- フェーズ追加 = GamePhase 派生 + PhaseId 発番（1以上。0=None 予約）+ `AddPhase` 1行
- BGM 切替・解析ログ = `PhaseChanged` 通知（Previous/Current/Payload）を購読

主要ファイル: `Assets/Script/Flow/Runtime/GameFlow.cs` / `GamePhase.cs` / `UnitySceneLoader.cs`。
テスト: `Assets/Script/Flow/Tests/Editor/GameFlowTests.cs`
