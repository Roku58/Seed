# 02. Seed.Hub — 基盤間メッセージとメッセージトレーサ

基盤同士（UI・キャラ・フロー等）が互いを知らずに連携するためのメッセージハブ。
**通知**（過去形・複数購読OK・購読者0でも成立）と**命令**（処理者は必ず1基盤）を型で区別する。
GameCore の EventHub とは別物（あちらは決定的ロジック内側・リプレイ記録対象）。

## 最小コードで試す

空 GameObject に付ける自作スクリプト1本で動く（シーン配置物なし）:

```csharp
using Seed.Hub;
using Seed.Hub.Contracts;
using UnityEngine;

/// <summary>通知（過去形・複数購読OK）。</summary>
public readonly struct EnemyDefeated : INotificationMessage
{
    public readonly int Id;
    public EnemyDefeated(int id) { Id = id; }
}

/// <summary>命令（処理者1基盤のみ）。</summary>
public readonly struct SaveGameCommand : ICommandMessage { }

public sealed class HubDemo : MonoBehaviour
{
    private readonly SubscriptionBag _bag = new SubscriptionBag();
    private MessageHub _hub;

    private void Awake()
    {
        _hub = new MessageHub();
        _hub.Subscribe<EnemyDefeated>(m => Debug.Log($"撃破: {m.Id}")).AddTo(_bag);
        _hub.SubscribeCommand<SaveGameCommand>(_ => Debug.Log("セーブ実行")).AddTo(_bag);
        _hub.Publish(new EnemyDefeated(7));         // 通知は Publish
        _hub.PublishCommand(new SaveGameCommand()); // 命令は PublishCommand
        _hub.PublishDeferred(new EnemyDefeated(8)); // 今の連鎖の外へ回す
    }

    private void LateUpdate() { _hub.Pump(); }      // フレーム末尾で遅延分を配達
    private void OnDestroy() { _bag.Dispose(); }
}
```

## メッセージトレーサ（エディタウィンドウ）

- メニュー **`Seed/Message Tracer`** で開く（Play 前に開くと「Play を開始すると自動で接続」の案内）
- 生きている全 MessageHub を毎秒再走査して自動接続し、**直近256件**の発行履歴と
  **型別頻度（上位8件）**を表示。行の書式は `[12.34s] Hub0 TypeName → N件配達`、
  配達失敗は `✕ 例外型: メッセージ` の太字
- ツールバー: 「記録」トグル / 「型別頻度」トグル / 「クリア」。右端に `Hub: N 記録: N/256`
- 記録されるのは型名・購読者数・時刻のみ（本体は渡らない＝ボックス化なし・本番コスト0）

## 知っておくべき規約とハマりどころ

- **Pump() は合成ルートの責務**。`Sample_GameFlowRunner` は LateUpdate で毎フレーム末尾に
  Pump を呼んでいる。自前ルートを作る場合も同様に毎フレーム末尾で呼ぶこと
- **メインスレッド専用**（開発ビルドはスレッド検査で HubException、リリースは検査なし）
- 規約違反は即例外: 命令型を Subscribe/Publish に渡す / 処理者不在の PublishCommand /
  SubscribeCommand の二重登録 — いずれも HubException（握り潰さない設計）
- 購読者の例外は全員への配達完了後に集約 HubException。ただし**トレーサを開いている間は
  DeliveryFailed 購読が付くため例外は投げ直されず観測のみ**になる
- 発行中に登録した購読はその発行では呼ばれない。入れ子発行の深さ上限は32（循環検出）
- 毎フレームの連続値（位置・HPゲージ等）はメッセージに流さず **ServiceRegistry**
  （インターフェース限定の同期問い合わせ台帳）で読む
- 依頼-応答は **RequestId 相関**: 永続ルートが持つ RequestIdSource インスタンスの
  `Next()` で発番（例: `_requestIds.Next()`。1プロセス1個が規約）→
  `XxxRequested(RequestId,…)` 命令 → 完了時に同 ID を載せた `XxxCompleted` 通知

## 増やすとき

- メッセージ型の追加: `Assets/Script/Hub/Contracts/Messages/` に readonly struct を1つ
  （`ChangePhaseCommand.cs` のように命令と結果通知を同居させるのが流儀）
- 共有 ID・問い合わせ窓口の追加: `Assets/Script/Hub/Contracts/`（ScreenId や ICharacterQuery の並び）

主要ファイル: `Assets/Script/Hub/Runtime/MessageHub.cs` /
`Assets/Script/Hub/Editor/MessageHubTracerWindow.cs` /
テスト `Assets/Script/Hub/Tests/Editor/HubTests.cs`・`HubGuaranteeTests.cs`
