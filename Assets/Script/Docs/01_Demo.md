# 01. 統合デモの起動と操作（Sample_GameFlowRunner）

ホーム→戦闘→ショップが繋がった、ほぼ全基盤の結合デモ。**最初にこれを動かす。**

## 起動手順（Unity エディタ）

1. シーンを開く（`Assets/Scenes/SampleScene.unity` でも新規シーンでも可。
   カメラ・ライトが無ければ Runner が自動生成する）
2. Hierarchy で空の GameObject を作成（GameObject > Create Empty。名前は任意）
3. Add Component → **Sample_GameFlowRunner** を追加（Inspector の設定項目は無し）
4. Play → 画面左上に「== ホーム ==」のテキストメニューが出れば成功

> **反応しない時**: `Sample_KeyboardReader` は Input System（`Keyboard.current`）直読み。
> Project Settings > Player > Active Input Handling が「Input System Package」か「Both」で
> あること（本プロジェクトは Both 設定済み）。
> **何も出ない時**: Runner のアタッチ忘れ。シーンには**未配置**なので手動で置く。

## キー操作一覧

物理キー→ActionId の割当は `Sample_KeyboardReader` で固定。**同じキーでも意味はフェーズごとに変わる**
（意味づけは各フェーズの仕事。リーダーはキー状態を写すだけ）。

| キー | ホーム | 戦闘 | ショップ |
|---|---|---|---|
| WASD | - | 移動 | - |
| [1] | 出撃: 草原（201・敵の攻撃ゆっくり） | 攻撃 | - |
| [2] | 出撃: 火山（202・敵の攻撃速い） | - | - |
| [3] | ショップへ | - | - |
| [4] | 出撃: 迷宮（203・自動生成 21×21） | - | - |
| [5] | 出撃: 市街（204・自動生成 31×31） | - | - |
| [G] | - | ガード（押している間） | - |
| [T] | - | 3D⇔2D Actor 切替 | - |
| [O] | - | 自動操縦の切替（AI 操作） | - |
| [P] | - | ポーズ切替 | - |
| [B] | - | ホームへ戻る（決着後・ポーズ中も有効） | ホームへ戻る |

生成ステージ（迷宮・市街）ではキーではなく**マーカーを踏む**（半径0.8の純C#距離判定・コライダー不要）:

- 緑マーカー = 出口 → ホームへ帰還
- 黄マーカー = 店 → ショップフェーズへ
- 紫マーカー = 宝箱 → 鬼人薬（攻撃+15 を 20 秒。1回で消滅）

## 見どころ（何が確認できるか）

- 被弾の瞬間に 0.06 秒のヒットストップ（`HitStopCommand` → 世界全体が静止）
- プレイヤーの HP が減るほど敵の攻撃間隔が延びる（メタAIの手心 → [11_AI.md](11_AI.md)）
- プレイヤー（青カプセル）の頭が常に敵を注視、3m 以内で左腕が敵へ伸びる、
  ポニーテールが移動・旋回で揺れる（→ [05_Motion.md](05_Motion.md)）
- [T] で 2D 立ち絵へ切替（位置・向きは引き継ぎ、行動は Idle から再出発）
- 同じ生成ステージへ再入すると**同じ地形・同じ配置**（決定性。シード = 700+StageId）
- Hierarchy でフェーズ入退場のたび「HomePhase」「Stage_Grassland」等のルートが生成/破棄される
- 決着でリザルト画面「討伐成功！」/「力尽きた…」→ [B] でホームへ

## ハマりどころ

- 自動操縦 [O] 中は WASD・[1]攻撃・[G]ガードは読まれない（[T][B][P] は有効）
- カメラ位置は Play 開始時に Runner が、生成ステージ（迷宮・市街）入場時に戦闘フェーズが
  上書きする（手置きしても Play 開始時に失われる。固定ステージ入場では動かさないため、
  生成ステージから戻った後は俯瞰位置のまま残る）
- 戦闘は Hunter/Monster の 1v1 固定（GameCore サンプル世界の既知の制約。
  生成ステージでも実体化される敵は最初の EnemySpawn の 1 体のみ）
- payload が 0 や不明な StageId のときは草原（Stage1）へフォールバック

## 拡張の入口

- ステージ追加 = `Sample_MasterCatalog.Build` に Spec 1件 + ホームにメニュー行（→ [13_Data.md](13_Data.md)）
- フェーズ追加 = GamePhase 派生 + `Sample_PhaseIds` 発番 + `_flow.AddPhase` 1行（→ [06_Flow.md](06_Flow.md)）
- 独自ボタン = `Sample_ActionIds` に 32〜63 帯で発番 + `Sample_KeyboardReader.Read` へ割当（→ [08_Input.md](08_Input.md)）

主要ファイル: `Assets/Script/App/Samples/Sample_GameFlowRunner.cs`（永続ルートの実例。
自作ゲームのルートはこれの骨格を写す）
