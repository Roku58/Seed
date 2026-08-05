# 17. テストの実行と読み方 — 全 215 件を仕様書として使う

[← 前: 16_World](16_World.md) | [索引](README.md) | [次: 索引へ戻る →](README.md)

## この章で分かること

- Unity の Test Runner で全 215 件の EditMode テストを実行し、失敗行から原因コードへ飛ぶ手順
- CI・バッチ検証用のコマンドライン実行と、終了コード・結果 XML の読み方
- 「仕様書として読む価値が高いテスト」がどこにあり、どの章の仕様を語っているか
- 自分の基盤にテストを足す手順（asmdef のコピー → 参照追加 → 決定性テストの型）
- なぜ Seed のテストが Play を必要としないのか——三大規約が生む「テスト可能性」

## 前提

01〜13 章を読んでいれば全テストが読めますが、この章だけを先に読むこともできます。前の章から使う知識は次の 3 点です。

- **MessageHub / ServiceRegistry**（[02_Hub](02_Hub.md)）: 命令に処理者が居なければ `HubException` になる契約。テストではこれが「配線漏れ検出装置」になります
- **Tick と Update の分離**（[03_Clock](03_Clock.md) 以降）: 状態は `Tick(deltaSeconds)` が進める。テストは Play せず手で `Tick` を呼びます
- **決定性**（[12_GameCore](12_GameCore.md)）: 同じ入力列から同じ結果が出ること。多くのテストがこれを直接アサートします

この章が初出の主な用語: NUnit、EditMode テスト / PlayMode テスト、asmdef、`defineConstraints`、テストダブル（Fake）、ゴールデンログテスト、パラメタライズドテスト、Fuzz テスト。

## 1. これは何か

Seed のテスト群は「回帰防止装置」と「実行可能な仕様書」を兼ねています。全 215 件で、うち **PlayMode テストは 0 件**——すべて EditMode です。実機ビルドもシーン再生も要りません。

なぜそうできるかというと、基盤の中身をほぼ純C#に寄せているからです。MonoBehaviour は「Unity から時間と入力をもらう端」だけに追いやってあり、判断（Behavior 遷移裁定・迷路生成・IK の数学・セーブ封筒の検証）はすべて `new` して呼べるクラスに入っています。

テストが無いと何に困るかは具体的です。

1. **決定性が静かに壊れる**。リプレイやセーブは「同じ入力から同じ結果」を前提にしています。ロジックのどこか一箇所で `UnityEngine.Random` や `DateTime.Now` を触ると、その場では何も起きず、後日リプレイが再現しなくなります。壊れた瞬間に赤くなる仕組みが必要です。
2. **仕様が人の記憶に置かれる**。「攻撃中のガード要求は棄却」「ヒットストップの重複要求は長い方が残る」といった細部は、文章にすると必ず実装とズレます。テストなら実装とズレた瞬間に落ちるので、**古くならない仕様書**になります。
3. **配線漏れが実行時まで見つからない**。Hub は命令の処理者不在を例外にする設計なので、`Assert.Throws<HubException>` を仕込んでおけば「基盤を消したのに命令が残っている」類の事故がテストで露見します。

> 📖 **用語 — EditMode テスト / PlayMode テスト**: Unity Test Framework の 2 種類の実行モード。EditMode テストはエディタのコンパイル済みアセンブリ上でそのまま走り、シーンの再生を伴いません（速い・数百件でも数秒）。PlayMode テストは実際に Play を開始してフレームを進めます（`[UnityTest]` とコルーチンが必要）。Seed は全アセンブリが Editor 限定なので PlayMode タブは空です。

> 📖 **用語 — NUnit**: .NET のテストフレームワーク。Unity Test Framework の土台で、`[Test]` を付けたメソッドを 1 件のテストとして実行し、`Assert.AreEqual` などの検証 API を提供します。本プロジェクトのバージョンは `com.unity.test-framework` 1.7.0 経由の NUnit 3 で、クラスへの `[TestFixture]` 属性は省略可（実際どのテストクラスも付けていません）。

## 2. 全体像

### 部品表

| 部品 | 役割 |
|---|---|
| Test Runner ウィンドウ | `Window > General > Test Runner`。ツリー表示・実行・失敗メッセージの閲覧 |
| テストアセンブリ（13 個） | `Assets/Script/<基盤>/Tests/Editor/Seed.<基盤>.Editor.Tests.asmdef` |
| NUnit（`nunit.framework.dll`） | `[Test]` / `[SetUp]` / `Assert` / `CollectionAssert` の提供元 |
| `UnityEngine.TestRunner` / `UnityEditor.TestRunner` | Unity 側のテスト実行基盤。テスト asmdef は必ずこの 2 つを参照 |
| テストダブル | `Character/Tests/Editor/FakeAvatar.cs`、`Input/Tests/Editor/FakeInputReader.cs` |
| 結果 XML | CLI 実行時の `-testResults` 出力（件数・失敗内容の機械可読形式） |

### テストアセンブリ一覧（全 215 件の内訳）

| テストアセンブリ | 件数 | 主に読める仕様 | 章 |
|---|---|---|---|
| `Seed.Hub.Editor.Tests` | 20 | 発行中購読/破棄・例外集約・Pump・循環検出・窓口登録の保証 | [02](02_Hub.md) |
| `Seed.Clock.Editor.Tests` | 6 | 倍速・ポーズ・ヒットストップ（重複は長い方が残る） | [03](03_Clock.md) |
| `Seed.Flow.Editor.Tests` | 7 | フェーズ遷移・同フェーズ再入・非同期ロード中の Tick 停止 | [04](04_Flow.md) |
| `Seed.Input.Editor.Tests` | 9 | 押下/離上エッジ検出・毎フレーム1回読み・Reset の幻押し抑制 | [05](05_Input.md) |
| `Seed.UI.Editor.Tests` | 7 | Push/Back 履歴・履歴上限・モーダルの LIFO・遷移中の再表示 | [06](06_UI.md) |
| `Seed.Data.Editor.Tests` | 4 | 台帳の出入り・型を跨いだ ID 重複検出・登録前の事前検証 | [07](07_Data.md) |
| `Seed.Character.Editor.Tests` | 48 | Behavior 遷移裁定・2フェーズリアクション・Actor 切替・名簿と検索 | [08](08_Character.md) |
| `Seed.Motion.Editor.Tests` | 21 | IK 解析解・‰イベント（ループ折り返し含む）・揺れものの安定性/FPS非依存 | [09](09_Motion.md) |
| `Seed.AI.Editor.Tests` | 7 | 評価器の最高スコア選択・指令によるゲート・同時実行トークン上限 | [10](10_AI.md) |
| `Seed.StageGen.Editor.Tests` | 12 | 迷路の全域連結・配置の距離帯・重み抽選の決定性・パス追加の無干渉 | [11](11_StageGen.md) |
| `Seed.Core.Editor.Tests` | 55 | 事象ハブ・巻き戻し・スナップショット・記録/リプレイ・Fuzz | [12](12_GameCore.md) |
| `Seed.Persistence.Editor.Tests` | 6 | 封筒の往復・1バイト改変/途中切れ/ゴミの拒否・不正キーの拒否 | [13](13_Persistence.md) |
| `Seed.App.Editor.Tests` | 13 | 合成ルートの E2E（Tick パイプライン順序・Hub↔GameCore 翻訳・リプレイ） | [01](01_Demo.md) |

内訳の作られ方も見ておくと役に立ちます。`[Test]` を付けた素のメソッドが 213 件、これに `[TestCase]` を 2 つ持つパラメタライズドメソッド 1 件（`ActionBattle_BlastBuildup_TriggersAtTolerance`）が 2 件分として数えられ、合計 215 件になります。テストを 1 メソッド足せば件数は 216 になるので、この数字は「今の緑の本数」であって固定値ではありません。

> 📖 **用語 — パラメタライズドテスト**: 1 つのメソッドに引数の組を複数与え、それぞれ独立した 1 件として実行する書き方。`[TestCase(2, false)]` / `[TestCase(3, true)]` のように書くと「2 ヒットでは発動しない・3 ヒットで発動する」を 2 件として個別に緑/赤判定できます。

### データの流れ

```
Unity のコンパイル
   │ defineConstraints: UNITY_INCLUDE_TESTS が立っている時だけ Tests アセンブリを含む
   ▼
13 個のテストアセンブリ（Seed.*.Editor.Tests）
   │ includePlatforms: ["Editor"] → プレイヤービルドには一切入らない
   ▼
Test Runner ウィンドウ  /  CLI の -runTests -testPlatform EditMode
   │ NUnit が [Test] メソッドを 1 件ずつ実行（[SetUp] は各テストの前に毎回走る）
   ▼
Assert 成功 = 緑 / 失敗 = 赤（Expected/But was + メッセージ引数 + スタックトレース）
   │
   ├─▶ ウィンドウ下部の詳細ペイン（ダブルクリックで該当行へジャンプ）
   └─▶ 結果 XML（total=/passed=/failed=）＋ プロセス終了コード
```

## 3. 動かして試す

### Test Runner ウィンドウ

1. Unity メニューバーの **`Window`** をクリック → **`General`** → **`Test Runner`** をクリック。「Test Runner」ウィンドウが開きます（Inspector の隣あたりにドッキングさせると使いやすい）
2. ウィンドウ上部のタブが **`PlayMode`** / **`EditMode`** の 2 つあります。**`EditMode`** をクリック。`PlayMode` タブは**空**が正常です（PlayMode テストは 0 件）
3. ツリーに 13 個のアセンブリノード（`Seed.AI.Editor.Tests` 〜 `Seed.UI.Editor.Tests`）が並びます。三角をクリックすると `MotionTests` などのクラス、さらに開くと `Spring_Gravity_DroopsTip` などのメソッドが 1 行 1 件で見えます
4. ウィンドウ左上の **`Run All`** をクリック。数秒でアイコンが順に緑（成功）へ変わり、全 215 件が緑になれば完了です
5. 一部だけ動かすときは、ツリーでノードを選んで **`Run Selected`**。アセンブリノードを選べばその基盤だけ、メソッド行を選べば 1 件だけ実行できます
6. 失敗を直したあとは **`Rerun Failed`**（前回赤かったものだけ再実行）が速いです

### 失敗行の見方

赤いノードをクリックすると、ウィンドウ下部の詳細ペインに次の 3 点が出ます。

- **失敗理由**: `Expected: 0.5f But was: 0.25f` のような期待値と実測値
- **Assert のメッセージ引数**: 本プロジェクトのテストは第 3・第 4 引数に日本語の意図を必ず書いています（例: `"折り返しは末尾→先頭の順で取りこぼさない"`）。ここが「何の仕様が壊れたか」の一次情報です
- **スタックトレース**: `at Seed.Motion.Tests.MotionTests.Spring_Gravity_DroopsTip () ... :313` の行を**ダブルクリック**すると、その行が IDE で開きます

`CollectionAssert.AreEqual` の失敗は「何番目の要素から違うか」を出すので、発火列や記録列のズレはここを読むのが最短です。

### コマンドラインから（CI・バッチ検証）

```bash
/Applications/Unity/Hub/Editor/6000.5.5f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -projectPath /Users/riku.matsuo/Unity/Seed -runTests -testPlatform EditMode -testResults /tmp/seed_results.xml -logFile /tmp/seed_tests.log
```

- 終了コード: 0=全緑 / 2=テスト失敗 / 1=コンパイルエラー（詳細は logFile の `error CS` を grep）
- 結果 XML の `total=/passed=/failed=` で件数確認
- エディタで同じプロジェクトを開いていると起動できません（プロジェクトロックのため先に閉じる）

> 📖 **用語 — `-batchmode` / `-nographics`**: Unity を GUI もグラフィックスデバイスも無しで起動する引数。CI マシンや SSH 越しで走らせるための指定です。EditMode テストしか無い本プロジェクトはこれで完走できます（PlayMode テストがあると描画を要する場合があります）。

## 4. コードで使う

### 最小例 — 既存アセンブリにテストを 1 本足す

```csharp
using NUnit.Framework;        // [Test] / Assert / CollectionAssert の提供元
using Seed.Clock;
using Seed.Hub;
using Seed.Hub.Contracts;

namespace Seed.Clock.Tests    // asmdef の rootNamespace と揃える
{
    /// <summary>自作の時間テスト。</summary>
    public sealed class MyClockTests
    {
        /// <summary>倍率 0.25 なら実 dt 0.02 がゲーム dt 0.005 になる。</summary>
        [Test]
        public void QuarterSpeed_ScalesGameDelta()
        {
            var hub = new MessageHub();                            // 命令を運ぶ Hub（02章）
            var clock = new GameClock();
            clock.Initialize(hub, new ServiceRegistry());           // 窓口登録つきで起動

            hub.PublishCommand(new SetTimeScaleCommand(0.25f));     // 方針は命令で渡す

            clock.Tick(0.02f);                                      // 実 dt を手で刻む（Play 不要の核心）

            Assert.AreEqual(0.005f, clock.ScaledDelta, 0.0001f, "ゲーム dt は 1/4");
            Assert.AreEqual(0.02f, clock.UnscaledDelta, 0.0001f, "実 dt は不変");
        }
    }
}
```

ファイルを `Assets/Script/Clock/Tests/Editor/MyClockTests.cs` に置いて保存するだけで、Unity のコンパイル後に Test Runner のツリーへ現れます。float の比較には**必ず第 3 引数の許容誤差**を付けます（誤差なしの `AreEqual` は僅差で落ちます）。

### 実戦例 — テストダブルで Unity 依存を切り離す

`CharacterActor` は表示物を `IAvatar` 契約でしか触りません。だから MonoBehaviour の代わりに「呼び出し痕跡を記録するだけの偽物」を渡せます。

```csharp
using NUnit.Framework;
using UnityEngine;

namespace Seed.Character.Tests
{
    public sealed class MyBehaviorTests
    {
        [Test]
        public void MoveIntent_ReachesAvatarAsSpeed()
        {
            var avatar = new FakeAvatar();                       // IAvatar の記録用実装（同フォルダに既存）
            var actor = new CharacterActor(new ActorKey(1), avatar);
            actor.AddBehavior(new IdleBehavior());               // 使う行動だけ登録すればよい
            actor.AddBehavior(new LocomotionBehavior(4f));       // 移動速度 4m/s
            actor.Activate(BehaviorKey.Idle);                    // 初期行動を指定して起動

            var intent = new CharacterIntent(Vector3.forward, false, BehaviorKey.None, 0);
            actor.SetIntent(in intent);                          // 意図を入れて
            actor.Tick(0.5f);                                    // 0.5 秒進める

            Assert.AreEqual(BehaviorKey.Locomotion.Value, actor.CurrentKey.Value, "移動へ遷移");
            Assert.Greater(actor.Pose.Position.z, 0f, "前方へ進んでいる");
            Assert.AreEqual(1f, avatar.LastSpeed, 0.001f, "全力の移動速度が Avatar へ届く");
            Assert.IsTrue(avatar.LastTransitionTo(BehaviorKey.Locomotion), "遷移通知も届く");
        }
    }
}
```

`FakeAvatar` は `Calls`（`"SetActive(True)"` などの文字列 `List<string>`）と `Transitions`、`ApplyPoseCount`、`Sink`（差し込まれたイベント受け口）を公開しています。「呼ばれた回数」「呼ばれた順序」「渡された値」まで検証できるので、Avatar 側の見た目を作らなくても契約の履行を確認できます。

> 📖 **用語 — テストダブル（Fake）**: 本物の代わりにテストへ差し込む代役。`FakeAvatar` は `IAvatar` を、`FakeInputReader` は `IInputReader` を実装しており、どちらも「値をセットできて痕跡を読める」だけの純C#です。基盤側が具体型ではなく契約（インタフェース）を要求している設計だから差し込めます。

## 5. 仕組み

### テスト asmdef の 8 行が何をしているか

テストアセンブリの正体は 25 行の JSON です。`Assets/Script/Motion/Tests/Editor/Seed.Motion.Editor.Tests.asmdef` の実物を分解します。

```json
{
    "name": "Seed.Motion.Editor.Tests",
    "rootNamespace": "Seed.Motion.Tests",
    "references": [ "Seed.Motion", "Seed.Character",
                    "UnityEngine.TestRunner", "UnityEditor.TestRunner" ],
    "includePlatforms": [ "Editor" ],
    "overrideReferences": true,
    "precompiledReferences": [ "nunit.framework.dll" ],
    "autoReferenced": false,
    "defineConstraints": [ "UNITY_INCLUDE_TESTS" ]
}
```

- `name` / `rootNamespace`: アセンブリ名と、新規スクリプトの既定 namespace。Test Runner のツリーに出る名前は `name` です
- `references`: **テスト対象の基盤 ＋ そのテストに必要な基盤 ＋ TestRunner×2**。Motion のテストが `Seed.Character` を参照しているのは `BehaviorKey` / `AvatarEventId` を使うためです
- `includePlatforms: ["Editor"]`: エディタ専用。プレイヤービルドに 1 バイトも混入しません
- `overrideReferences: true` ＋ `precompiledReferences: ["nunit.framework.dll"]`: `overrideReferences: true` にすると
  プリコンパイル済み DLL の自動参照が切れるため、そのときは `nunit.framework.dll` を必ず列挙します
  （両方とも既定値のままなら自動参照でコンパイルは通ります。既存 asmdef を写すのが安全）
- `autoReferenced: false`: 他のアセンブリから自動参照されない（テストが本体へ逆流するのを防ぐ）
- `defineConstraints: ["UNITY_INCLUDE_TESTS"]`: このシンボルが定義されている時だけコンパイル対象になる保険

> 📖 **用語 — asmdef（アセンブリ定義ファイル）**: Unity がスクリプトを別々の DLL へ分割するための設定ファイル。`references` に書いた相手のコードしか見えなくなるため、「Motion は Character を見てよいが Character は Motion を見ない」といった依存の向きを**コンパイラに強制させられます**。本プロジェクトが基盤ごとに asmdef を切っている理由がこれです。

> 📖 **用語 — `defineConstraints` / `UNITY_INCLUDE_TESTS`**: `defineConstraints` は「指定シンボルが定義されている時だけこのアセンブリをコンパイルする」条件。`UNITY_INCLUDE_TESTS` は Unity がテスト実行を含む文脈で自動定義するシンボルです。`includePlatforms` との二重の柵で、テストコードが製品ビルドへ入り込む経路を塞いでいます。

### 決定性テストが決定性を守る仕組み

決定性の検証は「同じことを 2 回やって完全一致を見る」の一点張りです。実例が 3 段階あります。

- **同じ入力列を 2 回**: `MotionTests.Spring_IsDeterministic` は `Vector3 Run()` というローカル関数で 1 回分の実行を丸ごと包み、`Assert.AreEqual(Run(), Run(), "float演算まで完全一致")` と書きます。ローカル関数で包むのは初期状態の共有を防ぐためです
- **同じシード・違うシード**: `StageGenTests.Generation_IsDeterministic` は「シード 42 を 2 回生成して全セル一致」だけでなく、「シード 43 では地形が違う」も同じテストで確認します。前者だけだと「常に同じ固定地形を返すバグ」が緑になってしまいます
- **記録 → リプレイ**: `SampleIntegrationTests.ActionBattle_Replay_ReproducesIdenticalResult` は本番実行の `InputJournal<TInput>` を別世界へ流し直し、記録列のハッシュ一致を見ます。`FuzzTests` はこれをランダム入力 120 手 × シード 100〜104 の 5 本で回します

> 📖 **用語 — ゴールデンログテスト**: 実行の全事象ログを「正解の記録」として丸ごと比較するテスト。個別の数値をアサートするより網羅的で、どこか 1 箇所の計算が変わっただけでも落ちます。`CommandBattle_SameSeed_ProducesIdenticalRecords` が整形済みログ列の `CollectionAssert.AreEqual` でこれをやっています。

> 📖 **用語 — Fuzz テスト**: ランダムな入力を大量に流し、例外・暴走ガード・不変条件の破れを探すテスト。Seed の `FuzzTests` は入力生成にも `DeterministicRandom` を使うので、落ちたシードを控えれば**その失敗を完全再現できます**。乱数を使うのにテストが再現可能、という設計です。

### 三大規約との関係

- **状態は Tick、艶は Update**: 状態を進めるのが `Tick(deltaSeconds)` という**引数で時間を受け取る純関数寄りのメソッド**なので、テストは `clock.Tick(0.02f)` や `actor.Tick(0.5f)` と書くだけで時間を完全に支配できます（`Time.deltaTime` を読む実装だと Play が必要になっていました）。逆に「艶」側（Update / LateUpdate の見た目補正）はテストしていません——テストできないものを艶へ閉じ込める、という切り分けが 215 件を EditMode に収めています
- **方針は App**: 基盤テストは「機構」だけを検証します。「敵が何秒ごとに攻撃するか」「死亡時にどの画面を出すか」といった方針入りの検証は `Seed.App.Editor.Tests`（`EnemyTimerLogicTests` / `HubIntegrationTests`）に集約されており、基盤テストを汚しません
- **命令の処理者は 1 基盤**: 命令の処理者不在・二重登録が `HubException` になる契約なので、`Assert.Throws<HubException>` がそのままテストの武器になります。`GameClockTests.Lifecycle_AndValidation` は `Dispose` 後に命令を投げて「窓口返却済み・処理者不在が握り潰されず検知される」ことまで確認しています

## 6. よくあるつまずき

- **症状**: Test Runner のツリーが空、または古いまま → **原因**: コンパイルエラーでアセンブリが生成されていない → **対処**: Console の `error CS` を先に潰す。CLI なら終了コード 1 とログの `error CS` が同じことを示します
- **症状**: 新しく書いたテストが一覧に出ない → **原因**: ファイルが `Tests/Editor/` 配下にない、または asmdef の `references` にテスト対象の基盤が無い → **対処**: 配置とアセンブリ名を確認。Inspector で .cs を選ぶと所属アセンブリ名が表示されます
- **症状**: `The type or namespace name 'NUnit' could not be found` → **原因**: `precompiledReferences` に `nunit.framework.dll` が無い、または `overrideReferences: false` のまま → **対処**: 2 つはセットで書く（既存 asmdef のコピーが最短）
- **症状**: float の比較が僅かに合わず落ちる → **原因**: `Assert.AreEqual(expected, actual)` を許容誤差なしで float に使っている → **対処**: 第 3 引数に許容誤差（本プロジェクトの慣習は 0.001f、厳密一致を要求する箇所は 1e-6f）
- **症状**: 1 件だけなら通るのに `Run All` で落ちる → **原因**: static 変数や一時ファイルをテスト間で共有している → **対処**: `[SetUp]` で毎回作り直し `[TearDown]` で片付ける（`SaveStoreTests` が一時ディレクトリでこの型を実演）
- **症状**: `PlayMode` タブに何も出ない → **原因**: 仕様。全テスト asmdef が `includePlatforms: ["Editor"]` → **対処**: そのままでよい。フレーム進行が本当に必要になった時だけ PlayMode アセンブリを新設する
- **症状**: 決定性テストが「同じ入力なのに」落ちる → **原因**: ロジック内で `UnityEngine.Random`・`DateTime.Now`・`Time.deltaTime`・`Dictionary<TKey, TValue>` の列挙順に依存している → **対処**: 乱数は `DeterministicRandom`、時刻は `ctx.NowMs`、刻みは引数で受け取る。列挙順に依存する集計は順序が定まる構造へ置き換える
- **症状**: CLI 実行が「プロジェクトが開けない」で止まる → **原因**: 同じプロジェクトを Unity エディタで開いたまま → **対処**: エディタを閉じてから実行する

## 7. 増やす・拡張する

新しい基盤 `Seed.Foo` にテストを足す手順です。

1. **フォルダを作る**: `Assets/Script/Foo/Tests/Editor/`
2. **asmdef をコピーする**: `Assets/Script/Motion/Tests/Editor/Seed.Motion.Editor.Tests.asmdef` を 2 に複製し、`Seed.Foo.Editor.Tests.asmdef` へリネーム
3. **3 箇所を書き換える**: `name` を `Seed.Foo.Editor.Tests`、`rootNamespace` を `Seed.Foo.Tests`、`references` の `Seed.Motion` / `Seed.Character` を自分が使う基盤へ。**`UnityEngine.TestRunner` と `UnityEditor.TestRunner` は消さない**。Hub 越しの検証をするなら `Seed.Hub` と `Seed.Hub.Contracts` を足します（`Seed.Clock.Editor.Tests` が最小の実例）
4. **テストクラスを書く**: `namespace Seed.Foo.Tests` の `public sealed class FooTests`。`[TestFixture]` は不要
5. **決定性テストを 1 本入れる**: 型は次のとおり（クラス名は自分のものへ置き換え）

```csharp
/// <summary>決定性: 同じ入力列を 2 回流すと結果が完全一致する。</summary>
[Test]
public void Foo_IsDeterministic()
{
    // ローカル関数で「1 回の実行」を丸ごと包む（初期状態の共有を防ぐ）
    List<int> Run()
    {
        var log = new List<int>();
        var subject = new FooEngine(seed: 42);   // 乱数源はシード固定
        for (var i = 0; i < 120; i++)
        {
            subject.Tick(1f / 60f, log.Add);     // 刻みは固定値（Time.deltaTime を使わない）
        }
        return log;
    }

    CollectionAssert.AreEqual(Run(), Run());     // 順序まで含めて完全一致
}
```

6. **必要なら代役を用意する**: 契約（インタフェース）を実装した Fake を同フォルダに置く。`FakeAvatar` / `FakeInputReader` が手本で、どちらも `[Test]` を持たない「道具ファイル」です
7. **仕様を日本語で残す**: 本プロジェクトの慣習として、各テストメソッドに `/// <summary>` の 1 行と、`Assert` の最終引数に日本語のメッセージを書きます。これが失敗時に最初に読まれる文章になります

拡張の指針もいくつかあります。純C#に寄せるほど EditMode で仕様が書けるので、**新機能はまず MonoBehaviour 抜きのクラスとして書き、Unity との接点を薄い端へ切り出す**のが結果的に最短です。判定の分岐が多いものは `[TestCase]` のパラメタライズドに、ログ全体の回帰を守りたいものはゴールデンログ比較に、入力の組み合わせ爆発が怖いものは Fuzz に——という使い分けが既存 215 件の中に実例つきで揃っています。

## 8. 関連ファイルとテスト

- `Assets/Script/Motion/Tests/Editor/Seed.Motion.Editor.Tests.asmdef` — テスト asmdef の手本（コピー元）
- `Assets/Script/Motion/Tests/Editor/MotionTests.cs` — 21 件。数学・イベント・決定性の書き味の手本
- `Assets/Script/Clock/Tests/Editor/Seed.Clock.Editor.Tests.asmdef` — Hub 参照つきの最小 asmdef
- `Assets/Script/Hub/Tests/Editor/HubGuaranteeTests.cs` — 13 件。Hub の保証（発行中購読・例外集約・Pump・循環検出）
- `Assets/Script/GameCore/Tests/Editor/Samples/SampleIntegrationTests.cs` — ゴールデンログ・物語アサーション・パラメタライズドの 3 手法
- `Assets/Script/GameCore/Tests/Editor/Samples/FuzzTests.cs` — ランダム入力 120 手 × 5 シードのリプレイ一致
- `Assets/Script/GameCore/Tests/Editor/Core/` — 55 件のうち中核。巻き戻し・スナップショット・再生キュー
- `Assets/Script/Character/Tests/Editor/FakeAvatar.cs` — テストダブルの手本
- `Assets/Script/Input/Tests/Editor/FakeInputReader.cs` — 入力の代役
- `Assets/Script/Persistence/Tests/Editor/SaveStoreTests.cs` — `[SetUp]` / `[TearDown]` で一時ディレクトリを扱う型
- `Assets/Script/App/Tests/Editor/HubIntegrationTests.cs` — 合成ルートの E2E（方針込みの検証はここへ）

[← 前: 16_World](16_World.md) | [索引](README.md) | [次: 索引へ戻る →](README.md)
