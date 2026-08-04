# 07. Seed.Clock — ゲーム時間（ポーズ・倍速・ヒットストップ）

`GameClock` は dt の供給源。毎フレーム最初に `Tick(実dt)` を呼び、
**ScaledDelta**（ゲーム進行用。ポーズ/ヒットストップ中は 0、倍率で伸縮）と
**UnscaledDelta**（実時間。メニュー・演出用）を供給する。
操作はすべて命令（処理者は GameClock のみ）。

## デモで確認する

- 戦闘中 [P] = ポーズ切替（`SetPausedCommand`）。戦闘パイプラインが止まるが [B] は効く
- 被弾の瞬間 0.06 秒のヒットストップ（`HitStopCommand`）——世界全体が一瞬止まる
- スロー/倍速（`SetTimeScaleCommand`）はデモにキー割当なし（命令自体は実装済み）

## 最小コード

```csharp
var clock = new GameClock();
clock.Initialize(hub, services);  // IGameClock を貸し出し、3命令の処理者になる

// 毎フレーム最初に（dt の加工が何より先）:
clock.Tick(Time.deltaTime);
myGameSystems.Tick(clock.ScaledDelta);    // ゲーム進行はこちら
myMenuAndFlow.Tick(clock.UnscaledDelta);  // ポーズ中も動かすものはこちら

// どこからでも命令1発:
hub.PublishCommand(new SetPausedCommand(true));
hub.PublishCommand(new SetTimeScaleCommand(0.1f));  // スローモーション
hub.PublishCommand(new HitStopCommand(0.06f));      // ヒットストップ

// 読み取りは ServiceRegistry 経由: services.Resolve<IGameClock>().IsPaused
```

## ハマりどころ

- ポーズ中はヒットストップも凍結（再開後に消化）。ヒットストップは重なったら長い方が残る（加算しない）
- `SetTimeScaleCommand` に負値は HubException
- GameClock を生成せずに命令を発行すると「処理者不在」の HubException
- どの系を Scaled / Unscaled で回すかはアプリの方針
  （デモ: 戦闘パイプライン=Scaled、GameFlow と UI=Unscaled）

主要ファイル: `Assets/Script/Clock/Runtime/GameClock.cs`、
契約と3命令は `Assets/Script/Hub/Contracts/IGameClock.cs`。
テスト: `Assets/Script/Clock/Tests/Editor/GameClockTests.cs`
