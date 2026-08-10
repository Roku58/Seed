# 12. GameCore — 決定的ロジックとリプレイ

[← 前: 11_StageGen](11_StageGen.md) | [索引](00_Roadmap.md) | [次: 13_Persistence →](13_Persistence.md)

## この章で分かること

- 「同じシード＋同じ入力列 → 必ず同じ結果」という規約（決定性）が何を可能にし、それを守るために何を禁じているか
- 3 つの ActionBattle サンプル（自動デモ / 対話型 / 3D）の起動手順・キー操作・Console 出力の読み方
- `InputJournal<TInput>` に入力を記録し、`byte[]` に保存し、再生してハッシュで一致を確かめる一連の手順
- `CoreHubBridge` が Hub の世界（メッセージ）と決定的世界（入力struct）をどう翻訳しているか
- 決定性を壊す典型パターンと、それを自動検知しているテストの位置

## 前提

先に次の章を読んでください。この章はそこで学んだ知識を使います。

- [02_Hub](02_Hub.md) — `MessageHub` の通知/命令と `Subscribe<T>`、契約アセンブリ `Game.<Title>.Contracts`。本章の `CoreHubBridge` が Hub 側と繋がる部分で使います
- [03_Clock](03_Clock.md) — `IGameClock` によるゲーム時間の供給。本章の「ロジック時間（`long` ミリ秒）」との境界変換に関わります
- [11_StageGen](11_StageGen.md) — 「同じシードなら同じ設計図」という決定的生成と、パスごとに乱数を分岐させる考え方。その土台となる乱数が本章の `DeterministicRandom` です

この章が初出の主な用語: 決定性、シード、入力ジャーナル、事象レコード、xorshift32、FNV-1a（安定ハッシュ）、`in` 引数、セクション／解決、ロックステップ、パーミル（‰）。

## 1. これは何か

GameCore はゲームロジックの中核です。中心の `Seed.Core` だけが `noEngineReferences: true`（UnityEngine を一切参照しない純C#）で、
周りの `Seed.Core.Presenter` / `Seed.Core.Samples` / `Seed.Core.Editor.Tests` は Unity 側の薄い端です
（`RecordPresenterBase` は MonoBehaviour、各 Runner は `SerializeField` / `OnGUI` / `GameObject.CreatePrimitive` を使います）。
看板となる規約はひとつだけです。

> **同じシード ＋ 同じ入力列 → 必ず同じ結果**

サンプル世界はモンハン風の Hunter / Monster 1v1（ActionBattle）で、肉質・会心・スタミナ・状態異常（爆破）・部位破壊・ガード性能・怒りといった実戦的な要素が全部載っています。

> 📖 **用語 — 決定性（determinism）**: 同じ入力を与えたら、いつ・どのマシンで・何回実行しても結果が完全に一致する性質。「だいたい同じ」では決定的とは言いません。1 回の乱数消費、1 ミリ秒の時刻差、`Dictionary` の列挙順のブレなど、たった 1 箇所の非決定性が最終結果を変えます。

> 📖 **用語 — シード（seed）**: 乱数列の出発点となる整数。決定的乱数はシードが決まれば以降の全出力が決まるため、本プロジェクトではシードを「乱数の設定値」ではなく**ロジックへの入力の一部**として扱います。

決定性が無いと、次の 4 つが原理的にできません。

1. **不具合の再現**。「たまに敵の攻撃が 2 回入る」を報告されても、同じ状況を作れなければ直せません。決定的なら「シード＋入力列」を添付してもらうだけで、開発者の手元で毎回同じバグが起きます。
2. **軽いリプレイ**。決定的でなければリプレイは画面の録画（動画）になり、容量も撮影負荷も跳ね上がります。決定的なら「シード＋入力列」だけで再生できるので、数百件の入力でも数 KB です。ゴースト走行・観戦・リザルトのハイライトが同じ仕組みに乗ります。
3. **通信対戦とサーバ検証**。入力だけを送り合って各自のマシンで同じ結果に到達させる方式（ロックステップ）や、サーバ側でクライアント申告を再計算して不正を弾く検証は、決定性が前提です。
4. **回帰テスト**。代表シナリオの結果をハッシュ 1 個で固定でき、「バランス調整のつもりが別の計算式まで壊した」を自動検知できます（ゴールデンログテスト）。

> 📖 **用語 — ロックステップ**: 通信対戦の実装方式。各クライアントは相手の**入力**だけを送り、ロジックは各自のマシンで計算します。座標や HP を送らないので通信量が極小になる代わり、どこか 1 箇所でも非決定的だと即座に desync（結果のズレ）が起きます。本基盤は現時点で通信機能を持ちませんが、`LogicInputFunnel` の呼び出し元を差し替えればこの方式へ載せられる構造にしてあります。

## 2. 全体像

### 部品表

| 部品 | 役割 |
|---|---|
| `LogicContext` | ロジック 1 回分の実行文脈。時刻 `NowMs`・`LogicRandom`・拡張置き場・セクション実行・スナップショットを持つ中心 |
| `DeterministicRandom` | ロジック専用の決定的乱数（xorshift32）。`Fork()` で独立した子ストリームを切り出せる |
| `InputJournal<TInput>` | 時刻付き入力の記録。`Record(timeMs, in input)` / `Count` / `this[i]` / `Clear()`、`Seed` と `Version` を持つ |
| `IInputCodec<TInput>` | 入力structと `BinaryWriter` / `BinaryReader` の変換。コアは入力の中身を知らないのでゲーム側が実装する |
| `InputJournalCodec` | ジャーナル ⇔ `byte[]`。`ToBytes` / `FromBytes`（封筒に magic・版数・シード・末尾ハッシュを付ける） |
| `StableHash` / `IHashableRecord` | プラットフォーム非依存の畳み込み（FNV-1a 32bit）と、レコードが全フィールドを固定順で流し込む契約 |
| `RecordLog<TRecord>` | 事象レコードの出力先。Presenter や翻訳表が「新着分だけ」を読む |
| `Sample_ActionWorld` | サンプル世界の合成ルート。通常実行とリプレイ再生が**まったく同じ構築手順**を通るための集約点 |
| `Sample_ActionDriver` | 進行役。`Execute(in Sample_ActionInput)` の `switch` がロジックへの唯一の入口 |
| `Sample_ActionInput` / `Sample_ActionInputKind` | リプレイ可能な入力struct（ID 参照）と、その 4 種の種別 |
| `LogicInputFunnel<TInput>`（App 層） | 「記録してから実行」を強制する入力の一本道 |
| `CoreHubBridge`（App 層） | Hub と決定的世界の双方向翻訳者 |

> 📖 **用語 — 事象レコード**: ロジックが「何が起きたか」を値型（struct）で書き出す出力形式。`Sample_Record` は時刻・種別・アクター ID・部位 ID・数値などのフィールドを持ちます。ロジック内で表示文字列を組み立てないのが規約で、文言づくりは Presenter の仕事です。だからレコードは表示から独立してハッシュ化でき、日本語を英語に直しても検証が壊れません。

### データの流れ

```
プレイヤー操作 / メタAI の采配 / フレーム経過時間
      │  ［App 層］Hub メッセージ → このゲームの入力struct へ翻訳
      ▼
LogicInputFunnel.Submit(in input)          ← ロジックを動かす唯一の道
      ├─① InputJournal.Record(nowMs, in input)   先に記録（クラッシュしても記録は残る）
      └─② Sample_ActionDriver.Execute(in input)  そのあと実行
              ├ AdvanceTime → ctx.AdvanceTime / スタミナ回復 / 効果の失効処理
              └ 攻撃・アイテム → ctx.BeginResolution() → ctx.RunSection(...)
                        └ EventHub 上のハンドラー合成（切れ味・弱点特効・ガード性能…）
                                 ▼
                        RecordLog<Sample_Record>（事象レコード列）
                              ├─▶ Presenter（画面・ログ・演出＝艶）
                              └─▶ RecordHubTranslator ─▶ Hub の通知メッセージ

［リプレイ］journal.Seed で世界を作り直し、journal[i].Input を先頭から Execute し直す
          → 両者の RecordLog を StableHash で畳み込み、値が一致すれば完全再現
```

## 3. 動かして試す

サンプルはどのシーンにも**未配置**です（`SampleScene.unity` にスクリプト参照がありません）。空の GameObject へ自分でアタッチするのが起動方法です。

### 3-1. 自動デモ — `Sample_ActionBattleRunner`

1. Unity でシーンを開きます（`Assets/Scenes/SampleScene.unity` でも `File > New Scene` でも可）
2. Hierarchy を右クリック > **Create Empty**
3. Inspector の **Add Component** > `Sample_ActionBattleRunner` を追加
4. Inspector に **Logic Seed**（既定 1041）と **Enable Core Trace**（既定 OFF）が出ます
5. **Play** を押す

Console の出力は次の順です（1 行目は固定文字列）。

- `======== Sample_ActionBattle（アクションバトル）デモ ========`
- 実況レコード（`Sample_ActionBattlePresenter.Format` 経由）。台本は t=0 で鬼人薬（攻撃+15 / 20 秒）→ 1 秒ごとに頭へ斬り上げ 3 連（3 発目で爆破の蓄積が閾値 30 に達して**爆破が発動 → 頭部破壊**という連鎖の連鎖が起きる）→ t=4.0s で尻尾回転を**ガード**（ガード性能 Lv2 でチップダメージ 0）→ +18 秒で**鬼人薬が失効**→ 溜め斬りで累計被ダメージが閾値を超えて**モンスターが怒り**→ 同じ斬り上げのダメージが下がる → 最後はスタミナ不足で**行動不可**
- `リプレイ検証 OK: hash=XXXXXXXX（入力列＋シードだけで完全再現できた）`

最後の 1 行が出れば決定性が成立している証拠です。壊れていると `リプレイ検証 NG: original=XXXXXXXX replay=YYYYYYYY（決定性が壊れている！）` になります。**Enable Core Trace** を ON にすると、続けて `---- コアトレース（N行）----` とセクションの出入り・イベント発火の内部動作が出ます。

Hierarchy は変化しません。GameCore は純C#で、GameObject を一切作らないからです（作るのは 3-3 の 3D デモだけ）。

### 3-2. 対話型 — `Sample_ActionBattleInteractiveRunner`

事前に **Project Settings > Player > Active Input Handling** を「Input Manager (Old)」か「Both」にしてください（旧 `Input` クラスを使うため。本プロジェクトは Both 済み）。手順は 3-1 と同じで、Add Component するクラスだけ差し替えます。Inspector には **Logic Seed**（既定 500）と **Monster Attack Interval Ms**（既定 4000）があります。

Play すると画面上部に HP・スタミナ、その下に直近 14 行のログが `OnGUI` で描かれ、`――― 狩り開始（シード 500）。[1][2][3]攻撃 [4]鬼人薬 [G]ガード ―――` が出ます。

| キー | 動作 | ジャーナルへの記録 |
|---|---|---|
| **[1]** | 斬り上げ → 頭 | `HunterAttack(SlashUp, Head)` |
| **[2]** | 斬り上げ → 翼 | `HunterAttack(SlashUp, Wing)` |
| **[3]** | 溜め斬り → 頭 | `HunterAttack(ChargedSlash, Head)` |
| **[4]** | 鬼人薬（攻撃+15 / 20 秒） | `UseDemonDrug(15, 20000)` |
| **[G]**（長押し） | ガード姿勢。4 秒ごとの尻尾回転をガードで受ける（構え中は攻撃・アイテム不可） | `MonsterAttack(TailSwipe, guarded: true)` |
| **[V]** | リプレイ検証（決着後も可） | — |
| **[R]** | 決着後リスタート（シード+1 で次の狩り） | — |

毎フレームの経過時間も `AdvanceTime` として記録されるので、「あなたが今遊んだプレイ」がまるごと入力列になっています。**[V]** を押すと `リプレイ検証 OK: hash=XXXXXXXX（あなたのプレイは入力列だけで完全再現できる）` が画面と Console に出ます。

### 3-3. 3D 統合 — `Sample_ActionBattle3DRunner`

空の GameObject に Add Component して Play すると、Hierarchy に `Ground`（Plane）・`Player`（青カプセル＋子の `AttackHitbox`）・`Monster`（灰キューブ＋子の `Head` 赤球 / `Wing`）がコードで生成されます（`Main Camera` は無ければ作られ、既にある場合も俯瞰位置へ動かされます。`Directional Light` はシーンに Light が 1 つも無いときだけ作られます）。操作は **WASD** 移動 / **[1]** 斬り上げ / **[3]** 溜め斬り / **[4]** 鬼人薬 / **[G]** ガード / **[R]** 決着後リスタート。攻撃の有効フレームだけヒットボックスが有効化され、当たった部位（頭 / 翼）で肉質が変わります。

このランナーは `InputJournal` を持たず `Driver` を直接呼ぶため、**[V] のリプレイ検証はありません**。3D 当たり判定とロジックの結合を見るためのデモという位置づけです。

### 3-4. テストで確認する

**Window > General > Test Runner** の **EditMode** で `Seed.Core.Editor.Tests` を実行すると、決定性まわりが緑になります。

- `ActionBattle_Replay_ReproducesIdenticalResult` — 記録 → 再生の一致
- `InputJournalCodec_RoundTrip_ReproducesIdenticalResult` — `byte[]` 往復 → 再生の一致
- `InputJournalCodec_RejectsWrongVersion` — 版数違いは `null`（再生拒否）
- `ActionBattle_RandomInputs_NoExplosion_AndReplayAlwaysMatches` — Fuzz。シード 100〜104 の 5 本 × ランダム 120 入力で、例外・暴走ガードなしに完走し、かつ常にハッシュ一致
- `LogicInputFunnel_RecordsBeforeExecute`（`App/Tests/Editor/FoundationTests.cs`） — 実行時点で既に記録済みであること

## 4. コードで使う

### 最小例 — 記録 → リプレイ → ハッシュ照合

```csharp
using Seed.Core;
using Seed.Core.Samples.ActionBattle;

// 世界＋ジャーナルを構築（シードはロジックへの「入力」）
const uint seed = 1041u;
var world = new Sample_ActionWorld(seed, trace: null);
var journal = new InputJournal<Sample_ActionInput>(32) { Seed = seed, Version = 1 };
world.Ctx.AddExtension(journal);   // VerifyReplay が ctx から引くので登録が必須

// ★規約: 記録してから実行（クラッシュしても記録が先に残る）
var input = Sample_ActionInput.HunterAttack(
    world.Registry.GetId(world.SlashUp), world.Registry.GetId(world.Head));
journal.Record(world.Ctx.NowMs, in input);
world.Driver.Execute(in input);

// リプレイ: 同じシードで世界を作り直し、入力列を先頭から流し直すだけ
var replay = new Sample_ActionWorld(journal.Seed, trace: null);
for (var i = 0; i < journal.Count; i++)
{
    replay.Driver.Execute(journal[i].Input);
}

// レコード列の StableHash が一致すれば完全再現（不一致＝決定性が壊れている）
bool match = Sample_ActionBattleDemo.HashRecords(world.Ctx)
          == Sample_ActionBattleDemo.HashRecords(replay.Ctx);
```

> 📖 **用語 — `in` 引数**: C# の引数修飾子。値型（struct）を**コピーせず読み取り専用の参照で渡す**指定です。`Execute(in input)` と書くと、フィールドの多い入力structでもコピー費用が乗らず、呼ばれた側が中身を書き換えられないことが型で保証されます。`Sample_ActionInput` は `readonly struct`（全フィールド不変）で、記録した入力が後から書き換わらないことも型で保証されています。

### 実戦例 — 一本道で流し、`byte[]` に保存して再生する

```csharp
using System.IO;
using Seed.App;
using Seed.Core;
using Seed.Core.Samples.ActionBattle;

const int LogicVersion = 1;   // 計算式・データ・入力形式を変えたら上げる
const uint Seed = 1041u;

var world = new Sample_ActionWorld(Seed, trace: null);
var journal = new InputJournal<Sample_ActionInput>(512) { Seed = Seed, Version = LogicVersion };
world.Ctx.AddExtension(journal);

// 入力の一本道。以降 Driver を直接叩かない（叩いた分だけ記録から漏れる）
void ExecuteInput(in Sample_ActionInput i) => world.Driver.Execute(in i);
var funnel = new LogicInputFunnel<Sample_ActionInput>(
    journal, ExecuteInput, () => world.Ctx.NowMs);

funnel.Submit(Sample_ActionInput.AdvanceTime(1000));   // ★時間前進も「入力」
funnel.Submit(Sample_ActionInput.HunterAttack(
    world.Registry.GetId(world.SlashUp), world.Registry.GetId(world.Head)));

// --- 保存: byte[] までがコアの責務。ファイルI/O・圧縮はゲーム側 ---
var codec = new Sample_ActionInputCodec();          // IInputCodec<Sample_ActionInput> の実装
byte[] bytes = InputJournalCodec.ToBytes(journal, codec);
File.WriteAllBytes(path, bytes);                    // 実戦では 13章の ISaveStore へ渡す

// --- 読み込み: 版数不一致・破損・改竄・途中切れは、すべて null（例外は投げない） ---
var loaded = InputJournalCodec.FromBytes(File.ReadAllBytes(path), codec,
    expectedVersion: LogicVersion);
if (loaded == null)
{
    return;   // 「このリプレイは再生できません」と表示して終わり。これが既定の安全動作
}

// --- 再生: 保存されていたシードで作り直し、入力列を流す ---
var replay = new Sample_ActionWorld(loaded.Seed, trace: null);
for (var i = 0; i < loaded.Count; i++)
{
    replay.Driver.Execute(loaded[i].Input);
}
```

## 5. 仕組み

### 決定性を成立させている 4 つの仕掛け

**1. 乱数を 1 本に絞る。** ロジックが引ける乱数は `ctx.LogicRandom`（`DeterministicRandom`）だけです。`System.Random` はランタイム実装差のリスクがあるため使いません。`UnityEngine.Random` はグローバル状態で、演出が 1 回抽選するだけで列がずれるため論外です。

> 📖 **用語 — xorshift32**: 32bit の内部状態をシフトと XOR だけで撹拌する擬似乱数生成器。実装が数行で、どの環境でも**ビット単位で同じ列**を出すため決定的ロジックに向きます。`DeterministicRandom.NextUInt` の 3 行（`x ^= x << 13; x ^= x >> 17; x ^= x << 5;`）がそれです。

内部状態は 0 になると以降ずっと 0 のままになるため、シード 0 は生成時に `2463534242u` へ置換されます（`RestoreState` も同様）。つまり `seed = 0` と `seed = 2463534242` は同じ列です。

演出用の抽選が必要なときは `Fork()` で子ストリームを切り出します。親を 1 回消費し、splitmix32 風の撹拌で親子の相関を断ってから子のシードにするので、子は決定的でありながら親の列を汚しません。11 章のステージ生成が「パスごとに `Fork`」しているのと同じ道具です。

> 📖 **用語 — パーミル（‰）**: 千分率。確率 30% は 300‰ です。浮動小数点の丸めを持ち込まずに確率・倍率を整数で表すため、本プロジェクトはロジックの割合を全部 ‰ の `int` で扱います（09 章の「正規化‰」はクリップ内の再生位置に同じ表記を使ったもので、別用途です）。

`Roll(chancePermille)` には見落としやすい規約があります。**0‰ 以下 / 1000‰ 以上のときは乱数を消費しません**。「絶対に起きない判定」を通すたびに列がずれるのを防ぐためで、消費回数まで含めて決定的にする設計です。裏返しに、ハンドラー補正で確率が動的に 0‰・1000‰ を跨ぐ仕様（命中率補正など）では `Roll` だと消費回数が状況で変わってしまうため、常に 1 回消費する `RollAlwaysConsume` を使います。**仕様ごとにどちらを使うか固定する**のが規約です。

**2. 時間を整数にする。** `LogicContext.NowMs` は `long` のミリ秒で、`AdvanceTime(deltaMs)` でしか進みません（負値は `ArgumentOutOfRangeException`）。`float` 秒を累積すると誤差が蓄積して再生時にずれるためです。Unity の `Time.deltaTime`（`float` 秒）との境界には `LogicTimeAccumulator` が立ち、ミリ秒未満の端数を次フレームへ繰り越して切り捨て誤差の蓄積を防ぎます。そして**時間前進そのものを入力として記録する**ので、再生は「先頭から同じ入力を流す」だけになります。

**3. 参照ではなく ID で指す。** 入力structは対象をオブジェクト参照ではなく `EntityRegistry` の `int` ID で持ちます（`Sample_ActionInput` は `Kind` / `MoveId` / `PartId` / `Flag` / `Value` / `Value2` の 6 フィールド。`0` は `EntityRegistry.None` 予約）。だからそのまま直列化できます。ID は `RegisterWithId` で明示登録し（Hunter=1 / Monster=2 / Head=10 / Wing=11 / SlashUp=100 / ChargedSlash=101 / TailSwipe=102）、登録順に結果が依存しないようにしています。

**4. 出力をレコード化してハッシュする。** 検証のハッシュは表示文字列ではなく `IHashableRecord.AddTo(ref StableHash)` が流し込んだフィールド値です。`Sample_Record` は 12 個のフィールドを固定順で `Add` します（順序もハッシュの一部）。

> 📖 **用語 — FNV-1a（安定ハッシュ）**: バイト列を「XOR して素数を掛ける」だけで畳み込む単純なハッシュ関数。実装が固定なのでプラットフォーム・実行ごとに値が変わりません。対して `string.GetHashCode()` は .NET のランタイムや起動ごとに値が変わる（ハッシュ DoS 対策の乱択化）ため、永続化・検証には**使用禁止**です。`StableHash` がこの役目を負います。

### 記録してから実行する、という順序

`LogicInputFunnel.Submit` の本体は 2 行で、順序に意味があります。

```csharp
_journal.Record(_nowMs(), in input);   // ① 記録
_execute(in input);                    // ② 実行
```

逆順にすると、実行中に例外やクラッシュが起きた入力がジャーナルに残らず、「落ちる直前の 1 手」が再現できません。バグ報告のリプレイでいちばん欲しいのはその 1 手です。この順序は `FoundationTests.LogicInputFunnel_RecordsBeforeExecute` で検証されています。

`InputJournal.Record` は時刻の昇順を強制し、過去の時刻が来ると `ArgumentException`（「入力の時刻が過去に戻っている」）を投げます。時刻取得を `Func<long>` として Funnel に 1 箇所だけ持たせているのは、呼び出し側ごとに違う時刻源を渡して昇順が崩れる事故を避けるためです。

### `byte[]` の封筒と「不正入力は必ず null」

`InputJournalCodec` の形式は `magic("SIMJ") / フォーマット版数 / ロジック版数 / シード / 件数 / (時刻ms + 入力)×件数 / 末尾ハッシュ` です。**フォーマット版数（現在 2）は直列化形式そのものの版**で、`journal.Version`（ロジックの版数）とは別物です。

`FromBytes` は外部ファイルを「壊れている・改竄されている」前提で扱い、次のすべてで `null` を返します: `null` 引数 / 長さ不足 / magic 不一致 / フォーマット版数不一致 / `expectedVersion` 指定時のロジック版数不一致 / 件数が負または残バイト数に入りきらない / 途中切れ / 末尾ハッシュ不一致。例外は呼び出し側へ漏れません。実装で工夫している点が 2 つあります。

- **解析前にハッシュを照合する**。1 バイトでも壊れていれば構文解析を始めません
- **件数の妥当性を検査する**。壊れた 1 個の整数で `new InputJournal(count)` すると巨大確保が起きるため、`件数 × 1件の最小バイト数` が残バイト数を超えたら弾きます

末尾ハッシュは `StableHash` をそのまま流用し、長さも混ぜてから畳み込みます（末尾のゼロ埋めで同一ハッシュにならないように）。

### Hub の世界と決定的世界をどう繋ぐか（`CoreHubBridge`）

GameCore は Hub すら参照しません（純度維持）。接続は App 層の `CoreHubBridge` による一方向参照（Bridge → Core）だけで行います。Bridge が担うのは「このゲームの意味づけ」で、構造は共通基盤に委譲されています。

**Hub → Core（命令の翻訳）**。`Initialize()` で `_hub.Subscribe<AttackRequested>(OnAttackRequested)` を張り、届いたメッセージを入力structへ翻訳して `_funnel.Submit` に流します。

- どちらかが死んでいれば無視。攻撃者 ID がハンターなら `Sample_ActionInput.HunterAttack(message.MoveId, partId)`、モンスターなら `MonsterAttack(message.MoveId, message.TargetGuarding)`
- `message.PartId == 0`（部位指定なし）は既定ターゲット（頭）の ID へ解決します
- 攻撃者がどちらでもない場合は、旧実装のように黙ってモンスター扱いせず `Debug.LogWarning` を出します（ゲーム構成の不整合）
- ガードしているかどうかの真実はメッセージに載って届きます。Bridge は入力状態の写しを持ちません

時間前進は `AdvanceTime(deltaMs)`（`TickPhase.LogicTime` から呼ばれる）が `_funnel.Submit(Sample_ActionInput.AdvanceTime(deltaMs))` を実行します。宝箱イベントの鬼人薬（`UseDemonDrug(15, 20000)`）も同じ一本道です。**この 3 つがすべて Submit を通ることで、統合デモのプレイもリプレイ互換になります。**

> ここが旧構成の欠陥だった箇所です。旧 Bridge はセクションを直叩きしており、統合経路のプレイだけが記録から漏れていました。「入力を処理するのは 1 箇所だけ」を Funnel という型で固定したのが現在の形です。

**Core → Hub（事象の翻訳）**。`RecordHubTranslator<Sample_Record, Sample_RecordKind>` が宣言的な翻訳表を持ちます。`Map` で翻訳し、`MapIgnore` で「意図的に流さない」を明示し、**全 10 種を表に書き切る**のが規約です。現在は `HitDamage` / `GuardChip` → `CharacterDamaged`、`StatusTriggered`（爆破でダメージ > 0 のとき）→ `CharacterDamaged`、`Defeated` → `CharacterDied` の 4 種を `Map`、残り 6 種を `MapIgnore`。表に無い種が来たら `Unmapped` イベント（`Action<TKind>`）経由で警告が出ます——黙って捨てないための穴です。`Drain()` は `TickPhase.Drain` から毎フレーム呼ばれ、新着分だけを翻訳します。

シードは App が決めます。`Sample_BattlePhase` は世界を `new Sample_ActionWorld((uint)(700 + stage.Id), …)` で作り、続けて `_bridge.Journal.Seed` に**同じ値**を代入しています（Bridge のコンストラクタでは `Seed = 0` で作られるため、この代入を忘れると再生時のシードが食い違います）。

> 📖 **用語 — セクション / 解決**: セクション（`Section<TInput, TResult>`）はロジックの処理単位で、`ctx.RunSection` で実行します。イベントハンドラーの中からも呼べるので、「爆破 → 部位破壊」のような連鎖をメインロジック無変更で表現できます。解決（resolution）は「攻撃 1 回・アイテム使用 1 回」といったひとまとまりの区切りで、進行役は入口で必ず `ctx.BeginResolution()` を宣言します。宣言を忘れて `RunSection` すると `LogicException` になり、また 1 解決あたりのイベント発火数に上限を設けて連鎖の暴走を検知します。

> 📖 **用語 — 拡張（extension）**: `ctx.AddExtension<T>(instance)` / `ctx.GetExtension<T>()` で型をキーに登録するゲーム固有状態の置き場（`RecordLog` / `EntityRegistry` / `InputJournal` など）。コアを非ジェネリックに保ちつつ型安全にゲーム固有の状態を持たせる仕組みです。未登録の型を `GetExtension` すると `LogicException` で早期に落ちます（合成ルートの構成ミス検知）。`ISnapshotParticipant` を実装した拡張は巻き戻しの参加者として自動登録されます。

### 三大規約との関係

- **状態は Tick、艶は Update**: ロジックの前進は Tick 側（`TickPhase.LogicTime`）だけで起きます。`RecordLog` を読んで画面・SE・カメラへ配るのは Presenter 側（`RecordPresenterBase` の `LateUpdate` 自動 Drain）で、そこは何を描いてもロジックに影響しません。だから「演出を足したら対戦結果が変わった」が構造的に起きません。
- **方針は App**: 「誰がいつ攻撃するか」「シードをいくつにするか」「リプレイをどこへ保存するか」は全部 App の判断です。GameCore は「入力 1 件を処理して事象を書き出す」だけの実行部に徹します。
- **命令の処理者は 1 基盤**: 入力を処理するのは `Sample_ActionDriver.Execute` ただ一つ、そこへ入る道は `LogicInputFunnel.Submit` ただ一つです。処理者を 1 つに絞ったからこそ「全入力がジャーナルを通る」が言い切れ、リプレイという看板が成立します。

## 6. よくあるつまずき

- **症状**: `リプレイ検証 NG: original=… replay=…` が出る → **原因**: ロジックに非決定的なものが混入（`UnityEngine.Random` / `System.Random` / `DateTime.Now` / `float` 秒の累積 / `Dictionary` の列挙順に依存した処理 / 演出用の抽選を `ctx.LogicRandom` から引いた） → **対処**: ロジック乱数は `ctx.LogicRandom` に一本化し、演出用は `Fork()` した子ストリームへ分離する
- **症状**: `LogicException`（「拡張 … が登録されていない（合成ルートを確認）」） → **原因**: `VerifyReplay` はジャーナルを `ctx.GetExtension` で引くのに `AddExtension` していない → **対処**: 世界を作った直後に `world.Ctx.AddExtension(journal)` を書く
- **症状**: `ArgumentException: 入力の時刻が過去に戻っている（記録順は時刻昇順であること）` → **原因**: 別の世界のジャーナルへ記録した、または時刻源が複数あって単調増加していない → **対処**: 記録は 1 世界 1 ジャーナル。`LogicInputFunnel` を使えば時刻源が `() => world.Ctx.NowMs` の 1 箇所に固定される
- **症状**: `InputJournalCodec.FromBytes` が常に `null` を返す → **原因**: 版数不一致（`expectedVersion`）／フォーマット版数の違う旧ファイル／末尾ハッシュ不一致（1 バイトの破損・改竄）／途中切れ → **対処**: 仕様どおりの再生拒否。`Version` を上げたなら旧リプレイは切り捨てるのが正しい運用（無理に読ませると別の結果を「正解」として表示してしまう）
- **症状**: 統合デモ（Bridge 経由）の試合だけリプレイが再現しない → **原因**: `Journal.Seed` の設定漏れ（既定 `0` のまま）で、世界のシードと食い違っている → **対処**: 世界生成に使った値をそのまま `_bridge.Journal.Seed` に代入する
- **症状**: `LogicException: BeginResolution を呼ばずにセクションを実行した: …` → **原因**: 進行役の入口で解決の開始を宣言していない → **対処**: `Driver` の各実行メソッドの先頭で `_ctx.BeginResolution()` を呼ぶ
- **症状**: 保存済みリプレイが一斉に再生できなくなった → **原因**: `RegisterWithId` の明示 ID を変えた、または入力structのフィールドを増やして `Sample_ActionInputCodec` の読み書き順が変わった → **対処**: 一度公開した ID は動かさない。形式を変えたときは `InputJournal.Version` を上げて旧リプレイを明示的に拒否する
- **症状**: 命中率補正のある仕様で稀にリプレイが一致しない → **原因**: `Roll` は 0‰ 以下 / 1000‰ 以上で乱数を消費しないため、補正で確率が境界を跨ぐと消費回数が変わる → **対処**: そうした判定は `RollAlwaysConsume` に固定する
- **症状**: シードを 0 にしたのに別のシードと同じ展開になる → **原因**: 仕様。シード 0 は内部で `2463534242u` に置換される → **対処**: 0 を「未設定」の意味で使わない
- **症状**: 3D ランナーで **[V]** を押しても何も起きない → **原因**: 仕様。3D ランナーはジャーナルを持たず `Driver` を直接呼ぶ → **対処**: リプレイ検証を試すなら対話型ランナーを使う
- **症状**: 対話型 / 3D でキー入力が効かない → **原因**: **Active Input Handling** が「Input System (New)」のみになっている（両ランナーは旧 `Input` クラスを使う） → **対処**: 「Both」か「Input Manager (Old)」にする

## 7. 増やす・拡張する

- **入力の種類を増やす**: ①`Sample_ActionInputKind` に列挙値を足す → ②`Sample_ActionInput` に `static` ファクトリを足す → ③`Sample_ActionDriver.Execute` の `switch` に分岐を足す → ④`Sample_ActionInputCodec` の `Write` / `Read` に**固定順**で読み書きを足す。④で形式が変わったら `InputJournal.Version` を上げる
- **検証対象の事象を増やす**: レコードに `IHashableRecord` を実装し、`AddTo` で全フィールドを固定順に `Add`（`Sample_Record.AddTo` が手本）。順序を変えると過去のゴールデンハッシュが全部変わる点に注意
- **リプレイをファイルへ保存する**: `IInputCodec<TInput>` を実装して `InputJournalCodec.ToBytes` / `FromBytes` を使い、`byte[]` の置き場は [13_Persistence](13_Persistence.md) の `ISaveStore` に渡す（ファイル I/O・圧縮はゲーム側の責務）
- **世界の構成を変える**: `Sample_ActionWorld`（`RegisterWithId` の明示 ID と、ハンドラーの合成ルート＝「登録しない仕様は存在しない」）
- **自動デモの展開を変える**: `Sample_ActionBattleScenario.BuildInputs`。フェーズごとのメソッドに入力を足すと、上から読んで狩りの流れが追える形が保てます
- **巻き戻し・先読み AI**: `LogicContext.CaptureAll()` / `RestoreAll(snapshot)` でコア（時刻・乱数状態・暴走ガードのカウンタ）＋購読＋`ISnapshotParticipant` を一括保存/復元。参加者の件数が Capture 時と違うと `LogicException`（対応関係が崩れて静かに壊れるのを防ぐため）。アロケーションを伴うので節目で使います
- **リプレイ再生 UI・通信対戦**: `LogicInputFunnel.Submit` の呼び出し元を差し替えるだけで、保存済み入力列やリモート入力が同じ経路に乗ります
- **演出用の乱数が必要なとき**: `ctx.LogicRandom` を直接使わず `Fork()` で独立した子ストリームを切り出す

## 8. 関連ファイルとテスト

- `Assets/Script/GameCore/Runtime/Core/LogicContext.cs` — 実行文脈（時刻・乱数・拡張・セクション・スナップショット）
- `Assets/Script/GameCore/Runtime/Numerics/DeterministicRandom.cs` — xorshift32・`Roll` / `RollAlwaysConsume` / `Fork`
- `Assets/Script/GameCore/Runtime/Replay/InputJournal.cs` / `IInputCodec.cs` / `InputJournalCodec.cs` — 記録と `byte[]` 変換
- `Assets/Script/GameCore/Runtime/Records/StableHash.cs` / `IHashableRecord.cs` / `RecordLog.cs` — 安定ハッシュと事象レコード
- `Assets/Script/GameCore/Runtime/State/EntityRegistry.cs` — ID 方式の台帳（`RegisterWithId` / `GetId` / `GetEntity<T>`）
- `Assets/Script/GameCore/Samples/ActionBattle/Demo/Sample_ActionBattleDemo.cs` — `RunDemo` / `VerifyReplay` / `HashRecords`
- `Assets/Script/GameCore/Samples/ActionBattle/Demo/Sample_ActionBattleScenario.cs` — 自動デモの台本
- `Assets/Script/GameCore/Samples/ActionBattle/Driver/Sample_ActionWorld.cs` / `Sample_ActionDriver.cs` — 合成ルートと進行役
- `Assets/Script/GameCore/Samples/ActionBattle/Replay/Sample_ActionInput.cs` / `Sample_ActionInputKind.cs` / `Sample_ActionInputCodec.cs` — 入力structと直列化
- `Assets/Script/GameCore/Samples/ActionBattle/Presenter/Sample_ActionBattleRunner.cs` / `Sample_ActionBattleInteractiveRunner.cs` — 自動デモ / 対話型
- `Assets/Script/GameCore/Samples/ActionBattle/View3D/Sample_ActionBattle3DRunner.cs` — 3D 統合デモ
- `Assets/Script/App/Foundation/LogicInputFunnel.cs` / `RecordHubTranslator.cs` — 入力の一本道とレコード翻訳表
- `Assets/Script/App/CoreHubBridge.cs` — Hub ⇔ 決定的世界の双方向翻訳
- テスト: `Assets/Script/GameCore/Tests/Editor/Samples/SampleIntegrationTests.cs`（同一シードの一致・リプレイ再現）/ `Assets/Script/GameCore/Tests/Editor/Core/SnapshotReplayTests.cs`（`byte[]` 往復・版数拒否・ID の登録順非依存）/ `Assets/Script/GameCore/Tests/Editor/Samples/FuzzTests.cs`（ランダム 120 入力 × 5 シードで常に一致）/ `Assets/Script/App/Tests/Editor/FoundationTests.cs`（記録が実行より先）

[← 前: 11_StageGen](11_StageGen.md) | [索引](00_Roadmap.md) | [次: 13_Persistence →](13_Persistence.md)
