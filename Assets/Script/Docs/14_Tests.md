# 14. テストの実行方法（全 215 件・EditMode）

全基盤が純C#中心なので、動作仕様はほぼすべて EditMode テストで確認できる
（実機・Play 不要。決定性・リプレイ一致・IK の数学・揺れものの数値挙動まで）。

## Unity エディタから

1. `Window > General > Test Runner` を開く
2. **EditMode** タブ → `Run All`
3. 全 215 件が緑になることを確認

特定基盤だけ見たいときはツリーからアセンブリ単位で実行
（Seed.Hub.Editor.Tests / Seed.Character.Editor.Tests / Seed.Motion.Editor.Tests / …）。

## コマンドラインから（CI・バッチ検証）

```bash
/Applications/Unity/Hub/Editor/6000.5.5f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -projectPath /Users/riku.matsuo/Unity/Seed -runTests -testPlatform EditMode -testResults /tmp/seed_results.xml -logFile /tmp/seed_tests.log
```

- 終了コード: 0=全緑 / 2=テスト失敗 / 1=コンパイルエラー（詳細は logFile の `error CS` を grep）
- 結果 XML の `total=/passed=/failed=` で件数確認

## 読みものとしてのテスト（仕様書代わりに読む価値が高いもの）

| テスト | 何の仕様が読めるか |
|---|---|
| `Hub/Tests/Editor/HubGuaranteeTests.cs` | 発行中購読・例外集約・Pump・循環検出の保証 |
| `GameCore/Tests/Editor/Samples/SampleIntegrationTests.cs` | 記録→リプレイの完全一致（決定性の実証） |
| `GameCore/Tests/Editor/Samples/FuzzTests.cs` | ランダム入力120×5シードでもリプレイ一致 |
| `Character/Tests/Editor/` | Behavior 遷移裁定・2フェーズリアクション・Actor 切替 |
| `Motion/Tests/Editor/MotionTests.cs` | IK 解析解・‰イベント発火・揺れものの安定性/FPS非依存 |
| `StageGen/Tests/Editor/StageGenTests.cs` | 迷路の全域連結・配置の距離帯・シード互換 |
| `App/Tests/` | 合成ルートの E2E（Hub↔GameCore 翻訳・ジャーナル・パイプライン順序） |

## 自分の基盤を足すときのテスト流儀

- テスト asmdef は `<基盤>/Tests/Editor/` に置き、本体と TestRunner×2 を参照
  （既存の `Seed.Motion.Editor.Tests.asmdef` をコピーして名前を変えるのが早い）
- 純C#に寄せるほど EditMode で仕様が書ける（MonoBehaviour は端に追いやる家風）
- 決定性が絡むものは「同じ入力列を2回流して完全一致」を1本入れる
