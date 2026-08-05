# 13. Seed.Persistence — セーブ/ロード（`byte[]` を壊さずディスクへ置く）

[← 前: 12_GameCore](12_GameCore.md) | [索引](README.md) | [次: 14_Tests →](14_Tests.md)

## この章で分かること

- 「何を保存するか（直列化）」はゲーム側、「安全に置くこと」は基盤側という役割分担の理由
- `ISaveStore` の 4 メソッド（`TrySave` / `TryLoad` / `Exists` / `Delete`）が全部 bool を返す契約の意味
- 封筒形式（magic `SEDS` / 版数 / ペイロード長 / FNV-1a ハッシュ）が破損・改変・途中切れをどう弾くか
- 原子的書き込み（一時ファイル → `File.Replace`）で「保存中にクラッシュしても前のセーブは無傷」になる仕組み
- 12 章のリプレイ `byte[]` を実際にファイルへ落とす手順と、`ServiceRegistry` 経由で `ISaveStore` を貸し出す配線

## 前提

先に [02_Hub](02_Hub.md) と [12_GameCore](12_GameCore.md) を読んでください。この章は次の知識を使います。

- **`ServiceRegistry`**: 「今の状態を知りたい／窓口を借りたい」を契約インターフェースで貸し借りする台帳（02 章）。`ISaveStore` はここに登録して使います
- **合成ルート（永続ルート）**: 基盤を new して配線する唯一の場所。デモでは `Sample_GameFlowRunner.Start`（02・04 章）
- **`InputJournal<TInput>` / `InputJournalCodec`**: リプレイの記録と `byte[]` 化（12 章）。本章の保存対象の代表例
- **`ActionId` / `InputRouter`**: 試用スニペットのキー入力に使います（[05_Input](05_Input.md)）

この章が初出の主な用語: 直列化、封筒形式、マジックナンバー、リトルエンディアン、FNV-1a、原子的書き込み、パス注入、`persistentDataPath`、`noEngineReferences`、版数マイグレーション。

## 1. これは何か

Seed.Persistence は「`byte[]` を安全にディスクへ置く」層です。セーブデータの中身（所持金・進行度・リプレイ）を**組み立てるのはゲームの仕事**で、この基盤は組み立て済みのバイト列を受け取り、壊れない形で書き、壊れていたら読まない、という一点だけを担当します。

> 📖 **用語 — 直列化（シリアライズ）**: オブジェクトやゲーム状態を、ファイルや通信に載せられる 1 本のバイト列（`byte[]`）へ変換すること。逆変換は復元（デシリアライズ）。Seed.Persistence は**この変換を一切行いません**——何が状態なのかを知っているのはゲーム側だけだからです。

無いと困ることは 3 つあります。

1. **書き込み途中のクラッシュでセーブが消える**。`File.WriteAllBytes` を保存ファイルへ直接行うと、書き込み中に電源が落ちた瞬間、既存のセーブは「中途半端に上書きされた読めないファイル」になります。前のデータもろとも失われます。本基盤は一時ファイルへ書き切ってから置換するため、失敗しても**前のセーブがそのまま残ります**。
2. **壊れたデータを読んで例外が飛ぶ**。ディスク上のデータは「外部から来るデータ」です。半端に書かれた・ビットが化けた・手で改変されたバイト列を素直に解析すると、配列外参照や巨大メモリ確保でゲームが落ちます。本基盤は開封時に全検証し、不正なら**例外ではなく false** で拒否します（12 章の `InputJournalCodec` と同じ契約）。
3. **key がそのままファイルパスになる危険**。ユーザー入力由来の名前でセーブすると `../../` を含む key でセーブ領域の外へ書けてしまいます。本基盤は key を英数字と `-` `_` `.` に限定します。

> 📖 **用語 — 契約（Try〜 で bool を返す形）**: 本プロジェクトの規約で、「失敗しうる操作は例外ではなく戻り値で失敗を伝える」形。`ISaveStore` の 4 メソッドはすべて bool 返しで、**例外を投げません**。呼び出し側は必ず戻り値を見る必要があります（見ないと保存失敗に気付けません）。

## 2. 全体像

### 部品表

| 部品 | 置き場所 | 役割 |
|---|---|---|
| `ISaveStore` | `Seed.Hub.Contracts` | 契約。`TrySave` / `TryLoad` / `Exists` / `Delete` の 4 メソッド（すべて bool・例外なし） |
| `FileSaveStore(directory, version = 1)` | `Seed.Persistence` | 本番用。封筒形式＋原子的書き込み。拡張子 `.sav` |
| `MemorySaveStore()` | `Seed.Persistence` | テスト・体験版・保存禁止プラットフォーム用。`Dictionary<string, byte[]>` にコピー保持。`Count` プロパティあり |
| `SaveEnvelope`（static） | `Seed.Persistence` | 封筒の封入・開封。`Wrap(version, payload)` / `TryUnwrap(bytes, out version, out payload)` |

`Seed.Persistence` は `noEngineReferences: true` の純C#アセンブリで、参照先は `Seed.Hub` と `Seed.Hub.Contracts` の 2 つだけです。

> 📖 **用語 — `noEngineReferences`**: アセンブリ定義（asmdef）の設定で、true にすると UnityEngine への参照が禁止されます。`Application.persistentDataPath` のようなエンジン API はこの基盤の中では**呼べません**——保存先ディレクトリを呼び出し側が文字列で渡すコンストラクタ注入になっているのはこのためで、副産物としてヘッドレスサーバや EditMode テストでも同じ実装がそのまま動きます。

### データの流れ

```
ゲーム側の状態（真実は Tick が持つ）
   │ 直列化（何を byte[] にするかはゲーム側の責務）
   ▼
byte[]  例: InputJournalCodec.ToBytes(journal, codec) のリプレイ
   │ ISaveStore.TrySave(key, data)   ← ServiceRegistry から借りた窓口
   ▼
FileSaveStore
   ├─ SaveEnvelope.Wrap ─▶ [magic|版数|長さ|本文|ハッシュ]
   ├─ slot1.sav.tmp へ全バイト書き込み（ここで失敗しても本体は無傷）
   └─ File.Replace で slot1.sav へ置換（初回だけ File.Move）
   ▲
   │ TryLoad
   ├─ File.ReadAllBytes → SaveEnvelope.TryUnwrap（magic・長さ・ハッシュを照合）
   ├─ 版数が store の version と一致するか
   └─ どれか1つでも欠ければ false（例外は投げない）
```

## 3. 動かして試す

**デモにセーブ/ロードは配線されていません**。`Sample_GameFlowRunner` 一式のどこにも `TrySave` / `TryLoad` の呼び出しはなく、`ServiceRegistry` へ `ISaveStore` を登録している箇所もコード上に存在しません。試す道は 2 つです。

### ルートA — テストで確かめる（コード 0 行）

1. **Window > General > Test Runner** を開く
2. **EditMode** タブを選ぶ
3. ツリーの `Seed.Persistence.Editor.Tests`（アセンブリ）から名前空間 `Seed.Persistence.Tests` をたどり、`SaveStoreTests` を選んで **Run Selected**
4. 6 件すべて緑になる: `Envelope_Roundtrip`（封入→開封で版数とペイロードが一致）/ `Envelope_RejectsCorruption`（1 バイト改変・途中切れ・ゴミ・null をすべて拒否）/ `FileStore_SaveLoadDelete`（往復・上書き・削除）/ `FileStore_RejectsCorruptedFile`（ディスク上の破壊を検知）/ `FileStore_RejectsInvalidKeys`（`../escape` と `a/b` は false、`slot-1_backup.v2` は true）/ `MemoryStore_BehavesLikeContract`

Play も実機も不要です（純C#なので一瞬で終わります）。テストは `Path.GetTempPath()` 配下のランダム名ディレクトリを使い、`TearDown` で消します。

### ルートB — 自作 1 本で触る

1. **Project ビューで `Assets` 直下を右クリック → Create > C# Script**、名前を `SaveDemo` にする
   （**`Assets/Script/App/Samples` には置かないこと**——`Seed.App` の asmdef は `Seed.Persistence` を参照していないためコンパイルエラーになります。`Assets` 直下は asmdef の外＝`Assembly-CSharp` で、`autoReferenced: true` の基盤アセンブリがすべて見えます）
2. 中身を §4 の最小例で置き換えて保存し、コンパイルの完了を待つ
3. **File > New Scene** で空シーンを作る
4. **GameObject > Create Empty** で空の GameObject を作る（Hierarchy に `GameObject` が 1 つ増える）
5. Inspector の **Add Component** で `SaveDemo` を追加（他には何も生成されません）
6. **Play** を押す。この時点では Console に何も出ません
7. **[3] キー**（`ActionId.Submit`）→ Console に `保存: True`
8. **[2] キー**（`ActionId.Interact`）→ Console に `読込: 42`
9. Play を止め、Finder で `~/Library/Application Support/DefaultCompany/Seed/` を開く（`companyName` / `productName` は Project Settings の値。Assets の外なので **Project ビューには現れません**）
10. `slot1.sav` が **17 バイト**で存在する（ヘッダ 12＋本文 1＋ハッシュ 4）。バイナリエディタで開くと先頭 4 バイトは `53 44 45 53`
11. 末尾の 1 バイトを書き換えて保存し、もう一度 Play → **[2]** → Console は `読込失敗（無い・壊れている・版数違い）`。例外は出ません
12. `slot1.sav` を削除して **[2]** → 同じく `読込失敗…`（存在しない key も false）

> 📖 **用語 — `Application.persistentDataPath`**: プラットフォームごとに用意された「アプリが書き込んでよい永続領域」の絶対パス（macOS は `~/Library/Application Support/<会社名>/<製品名>/`）。エディタ実行でも同じ場所を指します。`Assets` 配下にセーブを書くと配布ビルドで書けない・アセットデータベースを汚す、という二重の問題が起きるため使いません。

## 4. コードで使う

### 最小例 — `FileSaveStore` を直接使う

```csharp
using Seed.App;          // Sample_KeyboardReader（デモ用の入力リーダー）
using Seed.Input;        // InputRouter / ActionId
using Seed.Persistence;  // FileSaveStore
using UnityEngine;

/// <summary>[3]で保存・[2]で読込する最小デモ（空GameObjectにアタッチ）。</summary>
public sealed class SaveDemo : MonoBehaviour
{
    private InputRouter _input;
    private FileSaveStore _store;

    private void Start()
    {
        _input = new InputRouter(new Sample_KeyboardReader());
        // 保存先は呼び出し側が解決して渡す（基盤は純C#で Application を知らない）
        _store = new FileSaveStore(Application.persistentDataPath, version: 1);
    }

    private void Update()
    {
        _input.Tick(); // 1フレームにちょうど1回（2回呼ぶと「押した瞬間」が消える）

        if (_input.WasPressedThisFrame(ActionId.Submit))      // [3]キー
        {
            // 戻り値を必ず見る。例外は飛ばず、失敗も false で返るだけ
            Debug.Log("保存: " + _store.TrySave("slot1", new byte[] { 42 }));
        }

        if (_input.WasPressedThisFrame(ActionId.Interact))    // [2]キー
        {
            Debug.Log(_store.TryLoad("slot1", out var data)
                ? "読込: " + data[0]
                : "読込失敗（無い・壊れている・版数違い）");
        }
    }
}
```

### 実戦例1 — 合成ルートで貸し出す（`ServiceRegistry` 配線）

保存先の決定はエンジン依存なので合成ルートで行い、利用側には契約だけを見せます。`Sample_GameFlowRunner.Start` の「1. フェーズをまたいで生きる基盤」ブロックへ 1 行足すのが定位置です（この配線を入れる場合は `Seed.App` の asmdef の `references` に `"Seed.Persistence"` を追加してください。現状は未参照です）。

```csharp
using Seed.Hub.Contracts;  // ISaveStore（契約）
using Seed.Persistence;    // FileSaveStore（実装を知るのは合成ルートだけ）

// --- 永続ルート: 生成と登録 ---
_services = new ServiceRegistry();
_services.Register<ISaveStore>(                                    // 型引数は必須（後述）
    new FileSaveStore(Application.persistentDataPath, version: 1));

// 体験版・保存禁止プラットフォームなら実装だけ差し替える（利用側は無改修）
// _services.Register<ISaveStore>(new MemorySaveStore());
```

```csharp
// --- 利用側（フェーズや画面）: 契約しか知らない。Seed.Persistence を参照しない ---
if (_services.TryResolve<ISaveStore>(out var store) && store.Exists("slot1"))
{
    // 未登録でも落ちない書き方。必須なら _services.Resolve<ISaveStore>()（未登録は HubException）
    store.TryLoad("slot1", out var bytes);
}
```

`Register<TService>` の型引数を省略すると型推論が `FileSaveStore` を選び、`HubException`（サービスはインターフェースのみ登録可）になります。実装型で貸し借りさせない検査が入っているためです。

### 実戦例2 — 12 章のリプレイをファイルへ落とす

12 章の `InputJournalCodec.ToBytes` が返す `byte[]` を、そのまま `ISaveStore` へ渡すだけでリプレイファイルになります。`ISaveStore` は中身を一切解釈しないので、この 2 層はきれいに繋がります。

```csharp
using Seed.Core;           // InputJournal / InputJournalCodec / IInputCodec
using Seed.Hub.Contracts;  // ISaveStore

/// <summary>リプレイの保存・読込（ゲーム側のヘルパ。基盤には置かない）。</summary>
public static class ReplayArchive
{
    /// <summary>リプレイを保存する。保存できたら true。</summary>
    public static bool Save<TInput>(ISaveStore store, string key,
        InputJournal<TInput> journal, IInputCodec<TInput> codec) where TInput : struct
    {
        // ToBytes は magic "SIMJ"＋形式版数＋ロジック版数＋シード＋件数＋末尾ハッシュ込みの byte[]
        var bytes = InputJournalCodec.ToBytes(journal, codec);
        return store.TrySave(key, bytes);   // ここで封筒に包まれ、原子的に置換される
    }

    /// <summary>リプレイを読む。無い・壊れている・版が違うなら null。</summary>
    public static InputJournal<TInput> Load<TInput>(ISaveStore store, string key,
        IInputCodec<TInput> codec, int expectedVersion) where TInput : struct
    {
        if (!store.TryLoad(key, out var bytes))
        {
            return null;                    // 存在しない・封筒の検証に失敗・保存形式の版数違い
        }
        // 中身の検証はコーデック側の担当（ロジック版数が違えば null＝再生拒否）
        return InputJournalCodec.FromBytes(bytes, codec, expectedVersion);
    }
}
```

```csharp
// 呼び出し側（例: リプレイ検証つきの戦闘フェーズ）
var codec = new Sample_ActionInputCodec();                       // ゲーム側が用意する変換器
ReplayArchive.Save(store, "replay.latest", journal, codec);      // 決着時に保存
var replay = ReplayArchive.Load(store, "replay.latest", codec, expectedVersion: 1);
if (replay != null)
{
    // journal.Seed と同じシードで作り直し、入力列を先頭から流せば同じ結果が再現する（12章）
}
```

`Sample_ActionInputCodec` は `Seed.Core.Samples`（`autoReferenced: false`）にあります。使う側の asmdef から明示参照するか、自分のゲーム用 `IInputCodec<TInput>` 実装を書いてください。

## 5. 仕組み

### 封筒形式（`SaveEnvelope`）

`Wrap` が作るバイト列は次の並びです（合計 = 16 + ペイロード長）。

| 位置 | 大きさ | 内容 |
|---|---|---|
| 0 | 4 | マジックナンバー `0x53454453` |
| 4 | 4 | 版数（`FileSaveStore` のコンストラクタで渡した値） |
| 8 | 4 | ペイロード長 |
| 12 | N | 本文（ゲームが作った `byte[]` そのまま） |
| 12+N | 4 | FNV-1a ハッシュ（**位置 0 から 12+N までの全バイト**が対象） |

> 📖 **用語 — マジックナンバー（magic）**: ファイル先頭に置く固定の識別子。「これは自分の形式のファイルか」を最初の 4 バイトで判定し、無関係なファイルの解析を始めないための番犬です。定数 `0x53454453` は上位バイトから読むと `S` `E` `D` `S`（Seed Save）ですが、書き込みはリトルエンディアンなので**ディスク上の並びは `53 44 45 53`**（テキスト表示では `SDES`）になります。

> 📖 **用語 — リトルエンディアン**: 多バイト整数を「下位バイトから先に」並べる方式。`SaveEnvelope` は `WriteInt` / `ReadInt` を自前で持ち、常にこの並びで読み書きします。プラットフォーム標準の並びに依存しないため、Windows で保存した `.sav` を他環境で読んでも同じ結果になります。

> 📖 **用語 — FNV-1a（32bit）**: `hash = (hash ^ byte) * 16777619` を初期値 2166136261 から全バイトに繰り返す高速なハッシュ関数。暗号強度はありません（改造ツール対策にはならない）が、目的は**破損検知**なので十分です。12 章の `StableHash` と同じ畳み込みで、実行やプラットフォームで値が変わりません。

`TryUnwrap` は上から順に、(1) null と最小長（16 バイト）、(2) magic 一致、(3) ペイロード長が負でなく**ファイル全長とちょうど一致**すること、(4) ハッシュ一致——を確認し、1 つでも外れたら false を返します。(3) が「途中切れ」と「末尾にゴミが付いた」を弾き、(4) が 1 ビットの化けを弾きます。ハッシュ対象にヘッダを含めているので、版数フィールドだけを書き換える改変も**破損として**検知されます。

### 原子的書き込み（`FileSaveStore.TrySave`）

> 📖 **用語 — 原子的（アトミック）な書き込み**: 「完全に成功した状態」と「何も起きていない状態」の中間が観測されない書き込み。ファイルの内容を直接上書きすると中間状態が観測できてしまうため、**別ファイルへ書き切ってから名前を差し替える**のが定石です。名前の差し替え（`File.Replace`）はファイルシステムが不可分に行います。

手順は次のとおりです。

1. `SaveEnvelope.Wrap(_version, data)` で封筒に包む
2. `slot1.sav.tmp` へ `File.WriteAllBytes`（本体 `slot1.sav` にはまだ触らない）
3. 本体が既にあれば `File.Replace(tmp, 本体, destinationBackupFileName: null)`、無ければ `File.Move`
4. 例外（`IOException` / `UnauthorizedAccessException`）は捕まえて false

2 の途中で電源が落ちても、失われるのは `.tmp` だけで本体は前の内容のまま読めます。3 まで到達すれば新しい内容に切り替わります。「半分だけ新しいセーブ」は原理的に生まれません。

### 三大規約との関係

- **状態は Tick、艶は Update**: 保存対象の真実はロジック（Tick 側）にあります。Seed.Persistence は Tick も Update も持たない**イベント駆動の I/O 層**で、呼ばれたときだけ動きます。裏返すと、保存する値は必ず Tick 側の状態から取ること——Update 側の見た目（補間途中の座標など）を直列化すると、ロードした世界がロジックと食い違います
- **方針は App**: どの key に保存するか、いつ保存するか（オートセーブの間隔）、版数が上がったとき古いセーブをどう扱うか——これらは全部ゲームの方針で、App が決めます。基盤は「版数が違えば false」までしか面倒を見ません
- **命令の処理者は 1 基盤**: `ISaveStore` は `ServiceRegistry` で貸し出す窓口であり、メッセージ（命令）ではありません。「保存して」を命令メッセージにする設計にする場合も、それを購読して `TrySave` を呼ぶ担当は 1 箇所に絞ってください。複数箇所から同じ key へ同時に書くと、原子的置換の恩恵（どちらか一方が完全に残る）はあっても**どちらが残るかは決まりません**

### 二重に検証されるリプレイ

実戦例2 のリプレイは、外側（`SaveEnvelope` の magic `SEDS`・ハッシュ）と内側（`InputJournalCodec` の magic `SIMJ`・形式版数・ロジック版数・件数妥当性・ハッシュ）の 2 枚の封筒に包まれます。冗長に見えますが役割が違います。外側は**ディスク由来の破損**を解析前に落とし、内側は**形式と版の不整合**を落とします。どちらも例外を投げず、false / null で返るため、呼び出し側の分岐は 2 段の `if` だけで済みます。

## 6. よくあるつまずき

- **症状**: 保存したはずなのにファイルが無い → **原因**: key に使えない文字（`/` `\` 空白・日本語など）が入っており、`TrySave` が黙って false を返した → **対処**: key は英数字と `-` `_` `.` のみ。**戻り値を必ず見る**（`Debug.Assert` でもよい）
- **症状**: `store.TrySave("saves/slot1", …)` でサブフォルダに分けたい → **原因**: 仕様。パス注入防止で区切り文字を禁止している → **対処**: `FileSaveStore` を分けたいフォルダごとに作る（コンストラクタでディレクトリは自動生成される）
- **症状**: アップデート後に既存プレイヤーのセーブが全部消えたように見える → **原因**: `version` を上げたため、`TryLoad` が版数不一致で false（破損と同じ扱い） → **対処**: 版数マイグレーションは上位層の責務。旧版の store も作って読み、新版で書き直す
  > 📖 **用語 — 版数マイグレーション**: セーブ形式が変わったとき、旧形式のデータを新形式へ変換して引き継ぐ処理。`FileSaveStore` は「版が違えば読まない」までを保証し、変換はゲーム側に委ねます（変換規則は中身を知る者しか書けないため）
- **症状**: `MemorySaveStore` では通ったテストが `FileSaveStore` で落ちる → **原因**: `MemorySaveStore` は key 検証も封筒検証も**しない**（空でなければ何でも受ける） → **対処**: key の妥当性を確かめたいテストは `FileSaveStore` で書く。`MemorySaveStore` は契約の挙動（存在しない key は false 等）を模すだけの素通し実装
- **症状**: `ServiceRegistry.Register` で `HubException`（サービスはインターフェースのみ登録可） → **原因**: `Register(new FileSaveStore(...))` と書き、型推論が実装型を選んだ → **対処**: `Register<ISaveStore>(...)` と型引数を明示する
- **症状**: `Resolve<ISaveStore>()` で `HubException`（未登録） → **原因**: 合成ルートで登録していない（デモは未配線） → **対処**: 永続ルートで `Register<ISaveStore>` する。任意機能なら `TryResolve` を使う
- **症状**: 純C#の基盤側で `Application.persistentDataPath` が書けない → **原因**: `Seed.Persistence` は `noEngineReferences: true` → **対処**: 呼び出し側（App 層）で解決して文字列でコンストラクタに渡す。これは制約ではなく設計
- **症状**: `Assets/Script/App/Samples` に置いた自作スクリプトで `Seed.Persistence` が見つからない → **原因**: `Seed.App` の asmdef が `Seed.Persistence` を参照していない → **対処**: 参照を追加するか、asmdef の外（`Assets` 直下など）に置く
- **症状**: `.sav.tmp` が残っている → **原因**: 置換の直前でプロセスが落ちた → **対処**: 放置して問題なし。本体は無傷で、次回保存時に上書きされる
- **症状**: 「例外を投げない契約」なのに例外が出た → **原因**: 捕まえているのは `IOException` と `UnauthorizedAccessException` の 2 系統のみ。また**コンストラクタは契約の外**（ディレクトリが空文字なら `ArgumentException`、`Directory.CreateDirectory` 失敗もそのまま飛ぶ） → **対処**: store の生成は合成ルートで 1 回だけ行い、そこで try/catch する（保存不可環境なら `MemorySaveStore` へフォールバック）

## 7. 増やす・拡張する

- **保存先を変える（クラウド・DB・暗号化）**: `Assets/Script/Hub/Contracts/ISaveStore.cs` の 4 メソッドを実装した新クラスを作り、合成ルートの `Register<ISaveStore>` を差し替える。利用側は無改修。**例外を投げず bool で返す**契約だけは守ること（非同期 API を包む場合はこの層で待ち合わせるか、別契約を新設する）
- **セーブスロットを増やす**: key を変えるだけ（`slot1` / `slot2` / `auto`）。列挙は `Exists` の総当たりが確実です（`ISaveStore` に一覧取得はありません）
- **形式を変えたとき**: `new FileSaveStore(dir, version: 2)` と版数を上げる。旧版を読む必要があれば旧 version の store も作り、`TryLoad` が成功した方から変換して新版で `TrySave`
- **セーブデータの中身を作る**: 直列化はゲーム側。既存の実装例は `Assets/Script/GameCore/Runtime/Replay/InputJournalCodec.cs`（`BinaryWriter` で固定順に書く方式）と、その入力変換 `Assets/Script/GameCore/Samples/ActionBattle/Replay/Sample_ActionInputCodec.cs`
- **保存禁止プラットフォーム対応**: 合成ルートの分岐で `MemorySaveStore` を登録する。`Count` プロパティでテストから件数を検証できます
- **テストを足す**: `Assets/Script/Persistence/Tests/Editor/SaveStoreTests.cs` に追記。一時ディレクトリの用意と片付けは既存の `SetUp` / `TearDown` に乗るだけです（→ [14_Tests](14_Tests.md)）
- **オートセーブ**: 保存タイミングの方針は App。[03_Clock](03_Clock.md) の時間供給や [04_Flow](04_Flow.md) のフェーズ退場（`OnExit`）を契機にすると、フレーム途中の中途半端な状態を保存せずに済みます

## 8. 関連ファイルとテスト

- `Assets/Script/Hub/Contracts/ISaveStore.cs` — 契約（4 メソッド・すべて bool）
- `Assets/Script/Persistence/Runtime/FileSaveStore.cs` — 本番実装（封筒＋原子的書き込み＋key 検証）
- `Assets/Script/Persistence/Runtime/MemorySaveStore.cs` — メモリ実装（コピー保持・`Count`）
- `Assets/Script/Persistence/Runtime/SaveEnvelope.cs` — 封筒の封入・開封と FNV-1a
- `Assets/Script/Persistence/Runtime/Seed.Persistence.asmdef` — 純C#設定（`noEngineReferences: true`、参照は `Seed.Hub` / `Seed.Hub.Contracts`）
- `Assets/Script/GameCore/Runtime/Replay/InputJournalCodec.cs` — 保存対象を作る側（リプレイの `byte[]` 化）
- `Assets/Script/Hub/Runtime/ServiceRegistry.cs` — 貸し出し台帳（`Register` / `Resolve` / `TryResolve`）
- テスト: `Assets/Script/Persistence/Tests/Editor/SaveStoreTests.cs` — EditMode 6 件（封筒往復・破損拒否・往復と削除・ディスク破損検知・不正 key 拒否・メモリ実装の契約一致）。エンジン非依存なので Play 不要で回ります

[← 前: 12_GameCore](12_GameCore.md) | [索引](README.md) | [次: 14_Tests →](14_Tests.md)
