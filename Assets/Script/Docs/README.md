# Seed スタートアップガイド集（学習ロードマップ）

Seed の各サンプル・各基盤を「どう起動し、どう使い、どう増やすか」を章立てで学ぶガイド集です。
**章番号 = 学習順**になっており、01 から順に読み進めれば、前の章で学んだ概念だけを前提に
次の章が理解できるように構成しています。設計思想の全体図は [ARCHITECTURE.md](../ARCHITECTURE.md) を参照してください。

## このガイドの読み方

- 各章は「これは何か → 動かして試す → コードで使う → 仕組み → つまずき → 拡張」の順で進みます
- 専門用語の初出には注釈ブロックが付きます:
  > 📖 **用語 — 合成ルート**: アプリの起動時に各基盤を new して配線する唯一の場所のこと。
  > Seed では `Sample_GameFlowRunner` がその実例です。
- 順番どおりでなくても読めるよう、各章の冒頭に「前提となる章」を明記しています
- 山括弧を含む語（`Game.<Title>.Contracts` や `List<T>` など）はバッククォートで表記します

## まず動かす（5分クイックスタート）

1. 任意のシーンを開く（`Assets/Scenes/SampleScene.unity` でも新規でも可）
2. 空の GameObject を作り **Sample_GameFlowRunner** を Add Component
3. Play → 左上にホームメニューが出る → [1][2][4][5] で出撃・[3] でショップ
   （詳しい手順とキー一覧は [01_Demo.md](01_Demo.md)）

## 学習ロードマップ（章番号 = 読む順）

### 第1部 — まず触る

| 章 | ガイド | 読むと分かること |
|---|---|---|
| 01 | [01_Demo.md](01_Demo.md) | 統合デモの起動・全キー操作・何が見えるか |

### 第2部 — 土台（この4章で「永続ルート」= Runner の中身が全部読めるようになる）

| 章 | ガイド | 読むと分かること |
|---|---|---|
| 02 | [02_Hub.md](02_Hub.md) | 基盤同士の会話手段（通知/命令/問い合わせ）とメッセージトレーサ |
| 03 | [03_Clock.md](03_Clock.md) | ゲーム時間の供給（ポーズ・倍速・ヒットストップ） |
| 04 | [04_Flow.md](04_Flow.md) | ホーム→戦闘などのフェーズ遷移とステージ切替 |
| 05 | [05_Input.md](05_Input.md) | 入力の流れ（デバイス読み取り→エッジ検出→意味づけ） |

### 第3部 — 画面とデータ

| 章 | ガイド | 読むと分かること |
|---|---|---|
| 06 | [06_UI.md](06_UI.md) | 画面の登録・遷移・演出。HUD が更新される経路 |
| 07 | [07_Data.md](07_Data.md) | マスターデータの定義・検証・エディタ入力補助 |

### 第4部 — キャラクター

| 章 | ガイド | 読むと分かること |
|---|---|---|
| 08 | [08_Character.md](08_Character.md) | アクター制御（Agent/Actor/Behavior/Avatar） |
| 09 | [09_Motion.md](09_Motion.md) | アニメ再生・注視・IK（階段や坂の足IK）・揺れもの |
| 10 | [10_AI.md](10_AI.md) | キャラAI＋メタAI。自動操縦の仕組み |

### 第5部 — ワールドと最深部

| 章 | ガイド | 読むと分かること |
|---|---|---|
| 11 | [11_StageGen.md](11_StageGen.md) | ステージ自動生成（迷路・街・配置・アセット差し替え） |
| 12 | [12_GameCore.md](12_GameCore.md) | 決定的ロジックと記録/リプレイ（Seed の心臓部） |
| 13 | [13_Persistence.md](13_Persistence.md) | セーブ/ロード（リプレイの保存とも接続） |

### 第6部 — 演出と規模

| 章 | ガイド | 読むと分かること |
|---|---|---|
| 14 | [14_Cameras.md](14_Cameras.md) | カメラ制御（FPS/TPS 切替・複数カメラの管理と合成。Cinemachine 駆動） |
| 15 | [15_Pooling.md](15_Pooling.md) | オブジェクトプール（生成/破棄のコストと GC を抑える） |
| 16 | [16_World.md](16_World.md) | 原点回帰（広大なフィールドでの座標精度の維持） |

### 第7部 — 品質と運用

| 章 | ガイド | 読むと分かること |
|---|---|---|
| 17 | [17_Tests.md](17_Tests.md) | 全テストの実行方法と「仕様書として読む」案内 |
| 18 | [18_Libraries.md](18_Libraries.md) | 外部ライブラリ8種の役割・依存の鉄則・セットアップ |

## サンプル一覧（シーンに置く MonoBehaviour）

どのシーンにもサンプルは**未配置**です。空 GameObject への手動アタッチが起動方法です。

| サンプル | シーンに置くもの | 何が試せるか | 章 |
|---|---|---|---|
| 統合デモ（ホーム/戦闘/ショップ） | `Sample_GameFlowRunner` | ほぼ全基盤の結合動作 | [01](01_Demo.md) |
| ActionBattle 自動デモ | `Sample_ActionBattleRunner` | 決定的ロジック＋リプレイ検証（全自動） | [12](12_GameCore.md) |
| ActionBattle 対話型 | `Sample_ActionBattleInteractiveRunner` | 手で遊んで [V] でリプレイ検証 | [12](12_GameCore.md) |
| ActionBattle 3D | `Sample_ActionBattle3DRunner` | 3D物理当たり判定との統合 | [12](12_GameCore.md) |
| CommandBattle 自動デモ | `Sample_CommandBattleRunner` | もう1つのサンプル世界（コマンドバトル）の台本実行 | [12](12_GameCore.md) |
| CommandBattle 対話型 | `Sample_CommandBattleInteractiveRunner` | コマンド選択で遊ぶ | [12](12_GameCore.md) |
| CommandBattle 表示 | `Sample_CommandBattleViewRunner` | レコード→表示の変換例 | [12](12_GameCore.md) |

## エディタメニュー（Unity メニューバー）

| メニュー | 何が開くか | 章 |
|---|---|---|
| `Seed/Message Tracer` | メッセージ発行の観測ウィンドウ（直近256件・型別頻度） | [02](02_Hub.md) |
| `Seed/Master Data Browser` | 全定義アセットの一覧・検証・空きID提案 | [07](07_Data.md) |
| `Seed/Master Data Validate` | ウィンドウ無しの一括検証（Console 出力） | [07](07_Data.md) |
| `Seed/Stage Palette` | 生成ステージのプレハブ↔役割の紐付け・検証 | [11](11_StageGen.md) |
| `Seed/Master Data Bake` | マスターデータを MasterMemory バイナリへ焼く | [18](18_Libraries.md) |
| `Seed/Setup/Install NuGet Packages` | NuGet ライブラリの一括セットアップ（依存解決込み） | [18](18_Libraries.md) |
| `Window > General > Test Runner` | 全 EditMode テスト | [17](17_Tests.md) |

## 全章共通の前提・規約

- **入力は Input System**（`Keyboard.current` 直読み）。デモ用 `Sample_KeyboardReader` は
  固定割当の最小実装で、実プロジェクトは `InputSystemReader`（.inputactions 駆動）を使います
- **ID 規約**（種類ごとに違うので注意）:
  - `ActionId`（入力ボタン）: 0=None / 1〜10 基盤予約 / **アプリ独自ボタンは 32〜63 帯**（64 以上はビットマスクに載らない）
  - `PhaseId` / `ScreenId` / `StageId` / `CharacterId`: 0=None 予約のみで、1 以上の値割り当てはアプリの裁量
    （デモは Phase 1〜3、Screen 1〜2、Stage 201〜204、Character 1〜2）
  - **型をまたぐ ID 一意性**はマスターデータ定義 ID 内の規約です（`MasterDataSet.ValidateGlobalIdUniqueness`。
    `EntityRegistry` の ID 空間が1本なので、ユニット 1〜99 / ステージ 201〜 のように帯で分けます → [07_Data.md](07_Data.md)）
- **三大規約**: 「状態は Tick、艶は Update」「方針は App」「命令の処理者は1基盤」——
  各章の「仕組み」節でその章に関わる形で都度説明します
