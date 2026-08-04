# Seed スタートアップガイド集

「どのサンプルをどう起動するか」「各基盤を最小で使うにはどう書くか」の索引。
設計思想・全体構造は [ARCHITECTURE.md](../ARCHITECTURE.md) を参照。

## まず動かす（5分コース）

1. 任意のシーンを開く（`Assets/Scenes/SampleScene.unity` でも新規でも可）
2. 空の GameObject を作り **Sample_GameFlowRunner** を Add Component
3. Play → 左上にホームメニューが出る → [1][2][4][5] で出撃・[3] でショップ（詳細は [01_Demo.md](01_Demo.md)）

## サンプル一覧（シーンに置く MonoBehaviour）

| サンプル | シーンに置くもの | 何が試せるか | ガイド |
|---|---|---|---|
| 統合デモ（ホーム/戦闘/ショップ） | `Sample_GameFlowRunner` | ほぼ全基盤の結合動作 | [01](01_Demo.md) |
| ActionBattle 自動デモ | `Sample_ActionBattleRunner` | 決定的ロジック＋リプレイ検証（全自動・Console出力） | [03](03_GameCore.md) |
| ActionBattle 対話型 | `Sample_ActionBattleInteractiveRunner` | 手で遊んで [V] で「自分のプレイ」をリプレイ検証 | [03](03_GameCore.md) |
| ActionBattle 3D | `Sample_ActionBattle3DRunner` | 3D物理当たり判定との統合 | [03](03_GameCore.md) |

※ どのシーンにもサンプルは**未配置**。空 GameObject への手動アタッチが起動方法。

## エディタメニュー（Unity メニューバー）

| メニュー | 何が開くか | ガイド |
|---|---|---|
| `Seed/Message Tracer` | メッセージ発行の観測ウィンドウ（直近256件・型別頻度） | [02](02_Hub.md) |
| `Seed/Master Data Browser` | 全定義アセットの一覧・検証・空きID提案 | [13](13_Data.md) |
| `Seed/Master Data Validate` | ウィンドウ無しの一括検証（Console 出力） | [13](13_Data.md) |
| `Window > General > Test Runner` | 全 EditMode テスト（215件） | [14](14_Tests.md) |

## 機能別ガイド

| # | ガイド | 基盤 | 一言 |
|---|---|---|---|
| 01 | [01_Demo.md](01_Demo.md) | 統合デモ | 起動手順と全キー操作 |
| 02 | [02_Hub.md](02_Hub.md) | Seed.Hub | 基盤間メッセージ（通知/命令）とトレーサ |
| 03 | [03_GameCore.md](03_GameCore.md) | GameCore | 決定的ロジック・記録/リプレイ |
| 04 | [04_Character.md](04_Character.md) | Seed.Character | アクター制御（Agent/Actor/Behavior/Avatar） |
| 05 | [05_Motion.md](05_Motion.md) | Seed.Motion | アニメ再生・姿勢・IK・揺れもの |
| 06 | [06_Flow.md](06_Flow.md) | Seed.Flow | フェーズ遷移・ステージ切替 |
| 07 | [07_Clock.md](07_Clock.md) | Seed.Clock | ポーズ・倍速・ヒットストップ |
| 08 | [08_Input.md](08_Input.md) | Seed.Input | 入力（Reader→Router→意味づけ） |
| 09 | [09_Persistence.md](09_Persistence.md) | Seed.Persistence | セーブ/ロード |
| 10 | [10_UI.md](10_UI.md) | Seed.UI | 画面の登録・遷移・演出 |
| 11 | [11_AI.md](11_AI.md) | Seed.AI | キャラAI＋メタAI・自動操縦 |
| 12 | [12_StageGen.md](12_StageGen.md) | Seed.StageGen | ステージ自動生成（迷路/街/配置） |
| 13 | [13_Data.md](13_Data.md) | Seed.Data | マスターデータと入力補助 |
| 14 | [14_Tests.md](14_Tests.md) | 全テスト | Test Runner と CLI 実行 |

## 共通の前提（全ガイド共通）

- **入力は Input System**（`Keyboard.current` 直読み）。デモ用 `Sample_KeyboardReader` は
  固定割当の最小実装で、実プロジェクトは `InputSystemReader`（.inputactions 駆動）を使う
- **ID規約**: 0 = None 予約。基盤予約 1〜99 / アプリ独自 100 以降
  （ステージは 201〜帯、独自 ActionId は 32〜63 帯）。型が違っても ID は全体で一意
- **規約**: 「状態は Tick、艶は Update」「方針は App」「命令の処理者は1基盤」——
  迷ったら [ARCHITECTURE.md](../ARCHITECTURE.md)
