# Seed GameCore（旧名: BattleFoundation）

> **名前の変遷**: BattleFoundation（v0.1）→ SimulationFoundation（v0.3）→ **GameCore（v0.4, 現名称）**。
> 実態が「バトル専用」ではなく汎用のロジック基盤であること、そしてチームの標準語彙（MVP）に
> 合わせることが理由。バトルは「サンプル2種」という位置づけ。
>
> **MVPとの対応（v0.4 で語彙を統一）**
>
> | 層 | 本基盤での実体 | 主な型 |
> |---|---|---|
> | **Model（Core/Logic）** | `Runtime/`（asmdef: `Seed.Core`）。UnityEngine非依存の決定的ロジック | `LogicContext` / `LogicEvent` / `ILogicEventHandler` / `Section` / `RecordLog` |
> | **Presenter** | `Presenter/`（asmdef: `Seed.Core.Presenter`）。レコード→Viewへの翻訳係 | `RecordPresenterBase` / `RecordPlaybackQueue` |
> | **View** | ゲーム側（uGUI・キャラ・カメラ・SE）。基盤には含まれない | — |
>
> オニオンアーキテクチャで読むなら、Runtime=Domain（最内周）、進行役=Application/UseCase、
> Presenter=Interface Adapter、Unity/View=最外周、レコード=Domain Event に相当する。

---

## 1. これまでの経緯（作業ログ）

本基盤は以下のセッション作業の集大成として作られた（2026-07-28, Cowork/Claude 作業）。

1. **講演調査**: CEDEC2026 ポケモンバトル講演のレポート記事（ファミ通・電ファミ・4Gamer）を調査し、エンジニア向けに解説
2. **PokemonBattleSample**: 講演の Section / Event / EventHandler 設計を Unity 想定 C# で再現（ターン制＋Z-A風リアルタイム制。Cowork成果物として別途zip納品済み）
3. **実戦投入レビュー**: 上記サンプルへの注意点・アドバイス・リファクタ案を整理（→ §4 に全反映）
4. **MonsterHunterBattleSample**: レビュー内容を反映してMH風アクションバトルへ流用実証（同じくzip納品済み）
5. **本基盤**: 2つの実装例から共通コアを抽出して本実装。サンプル2ジャンルを同一コア上に再実装
6. **配置変更**: 当初 `Packages/jp.co.gu3.battle-foundation`（UPM埋め込み）で実装したが、ユーザー指示により `Assets/Script/GameCore/` の通常コード群へ移設（asmdefは維持）
7. **コア標準機能の追加（v0.2相当）**: レビューで挙げた改善のうち通信対戦以外を基盤側に実装（→ §3.5）。本基盤はバトル専用ではなく「決定的な事象解決エンジン」であり、実績・クエスト・経済・ガチャ等にも同じ形で使える
8. **リネームと v0.3 機能追加（当時: SimulationFoundation）**: GameCore へ改名。購読スナップショット・構造化ハッシュ・明示ID登録・入力コーデック・レコードカーソル・Bind重複ガード・トレース上限・戦闘不能ガード・Fuzzテストを実装（→ §3.5 の後半）
9. **MVP語彙へ全面リネーム（v0.4）**: チーム標準のMVPに合わせ GameCore / Seed.Core / Logic* / Core* / Presenter へ統一。Presentationブリッジは「Presenter層」として正式に位置づけ

## 2. 講演の要点（設計の原典）

- バトルシステム＝「誰が何をするか（入力）を受け、何が起きるか（出力）を計算するシステム」
- 『サン・ムーン』で拡張の限界 → 専門チーム発足 → 『ソード・シールド』(2019)で基盤を全面再構築
- 要件は **構造化・拡張性・柔軟性・保守性**。前者2つが確立されれば後者2つは自然に満たされる
- **構造化**: ゲームロジック（何をどの順番で計算するか）と個別仕様（技・特性・道具）を分離。ロジックの最小単位が「**セクション**」（階層構造を持つ。例: ダメージ付与 ⊃ ダメージ計算 ⊃ 攻撃力決定…）
- **拡張性**: セクションが要所で**イベント**を発火し、個別仕様は**イベントハンドラー**として介入。既存コード無変更で追加でき、**登録しなければオミット**
- ハンドラーは任意のセクションを再帰的に呼べる → **割り込み**（せいでんき）と**連鎖**（クラボのみ）がロジック無変更で成立
- 運用事例: リアルタイムの『LEGENDS Z-A』もターン制と同一ロジック基盤。セクションを取捨選択（行動実行/技効果は不使用、命中判定はコリジョンに置換、発動判定/ダメージ付与は再利用）。「まもる」はハンドラーの差し替えだけで仕様変更に対応

出典:
- https://www.famitsu.com/article/202607/82439
- https://news.denfaminicogamer.jp/kikakuthetower/2607224z
- https://www.4gamer.net/games/897/G089789/20260728007/
- https://cedec.cesa.or.jp/2026/timetable/detail/s698585436538b/

## 3. フォルダ構成

```
Assets/Script/GameCore/
├─ Runtime/                  … Model（asmdef: Seed.Core / UnityEngine非依存）※役割別サブフォルダ
│  ├─ Core/                  … LogicContext / LogicContextConfig / LogicException / CoreSnapshot
│  ├─ Events/                … LogicEvent / EventHub / SubscriptionSnapshot / EventPool / EventScope /
│  │                            ILogicEventHandler / LogicEventHandlerBase
│  ├─ Sections/              … Section / SectionResult
│  ├─ Records/               … RecordLog / IHashableRecord / StableHash
│  ├─ Replay/                … InputJournal / IInputCodec / InputJournalCodec
│  ├─ State/                 … TimedCondition / ConditionMergePolicy / ConditionSet /
│  │                            EntityRegistry / FactoryRegistry
│  ├─ Numerics/              … Permille / DeterministicRandom
│  └─ Diagnostics/           … ICoreTraceListener / BufferedCoreTrace
├─ Presenter/                … Presenter層（asmdef: Seed.Core.Presenter）
│  ├─ RecordPresenterBase.cs … 即時Dispatch型の基底
│  └─ Playback/              … IPlaybackStep / TimedStep / RecordPlaybackQueue（順次再生）
├─ Samples/                  … 実装例2種（asmdef: Seed.Core.Samples / "Sample_"プレフィックス）
│  ├─ CommandBattle/         … Data / Actors / Records / Events / Sections / Handlers /
│  │                            Driver / Demo / Presenter の役割別サブフォルダ
│  └─ ActionBattle/          … 上記に加えて Replay/（入力・コーデック）
├─ Tests/Editor/             … Core/（コア・機能・スナップショット・再生キュー）と Samples/（統合・Fuzz）
└─ Docs/                     … STARTUP_GUIDE / IMPLEMENTATION_GUIDE
```

※ Runtime も上記の各機能ごとに1クラス1ファイルで分割済み（同名のジェネリック/非ジェネリック対
（SectionResult 等）と、密結合のインターフェース対（ILogicEventHandler）のみ同居を許容）。

asmdef（Seed.Core / .Samples / .Editor.Tests）は「コアのエンジン非依存の強制」と「Test Runner での実行」のために維持している。不要なら Runtime/Samples の asmdef は削除可（その場合 Tests は動かなくなる点に注意）。Samples は誤参照防止のため autoReferenced=false（本編コードからは参照不可。シーンへの Runner 配置は可能）。

## 3.5 コア標準機能（v0.2 追加分）と使いどころ

レビューで挙がった注意点・リファクタ案・拡張案のうち、通信対戦以外を基盤側に実装したもの。

| 機能 | 何が嬉しいか | 使い方の要点 |
|---|---|---|
| `EventScope<T>`（using自動返却） | イベントの返し忘れが構文的に不可能になる | `using (var s = EventScope<T>.Rent()) { ... ctx.Hub.Fire(s.Event, ctx); ... }` |
| 発火中Subscribeの遅延 | 「発火の途中で登録された人がその回に呼ばれるか」の曖昧さを排除。**その発火では呼ばれない**と確定 | 挙動変更が必要な場合のみ意識すればよい（既存サンプルは影響なし） |
| `ConditionMergePolicy` | バフ重ねがけの仕様（併存/延長/上書き/加算/無視）を仕様ごとに明示できる | `conditions.AddOrMerge(cond, ConditionMergePolicy.Extend, ctx.NowMs)` |
| `CoreSnapshot` + `RecordLog.TruncateTo` | 巻き戻し・AI先読しの土台。時刻と乱数状態を保存/復元 | ゲーム側状態のコピーは各ゲームの責務（アクター状態は一元化済みなので浅い） |
| `RollAlwaysConsume` | 確率が動的に0/100%を跨ぐ仕様でも乱数消費数が一定＝列ズレ防止 | 仕様ごとに Roll / RollAlwaysConsume のどちらを使うか固定する |
| `DeterministicRandom.Fork` | ユニット別・サブシステム別の独立乱数を決定的に派生。演出用乱数の分離にも | 親を1回消費して子を作る |
| `EntityRegistry` | レコードに参照でなくIDを入れられる＝セーブ・リプレイ保存・ログ送信が可能に | 合成ルートで全アクターを Register し、レコードにはIDを格納 |
| `FactoryRegistry<TKey,TArg,TProduct>` | 「キー→ハンドラー生成」の合成ルート部品（MoveBindingsの一般化） | ScriptableObjectデータ駆動化の受け口にもなる |
| `InputJournal<TInput>` | リプレイの土台（入力列＋シード＋版数） | 進行役が入力を受けるたび Record。再生は先頭から流し直すだけ |
| `StableHash` | ゴールデンログ・リプレイ検証用の環境非依存ハッシュ | `string.GetHashCode()` は使わないこと |
| `SectionResult` / `SectionResult<T>` | 「ゲーム的に正常な失敗」の標準形。例外は設計バグ専用に分離 | `if (result)` で判定可。失敗コードはゲーム側enumをintで |
| `ICoreTraceListener` / `BufferedCoreTrace` | 再入スキップ等「黙って起きること」を観測できる開発用の窓 | `ctx.TraceListener = new BufferedCoreTrace()`（null なら無コスト・行数上限つき） |

### v0.3 追加分

| 機能 | 何が嬉しいか | 使い方の要点 |
|---|---|---|
| `EventHub.CaptureSnapshot / RestoreSnapshot` | 巻き戻しの完全化。きのみ消費（解除）やまもる（登録）も元に戻る | `Sample_BattleSnapshot` が組み込み済みの実例。発火中は不可・節目で使う |
| `IHashableRecord`（構造化ハッシュ） | リプレイ検証が表示文言に依存しない。文言修正で検証が壊れない | レコードに `AddTo(ref StableHash)` を実装（全フィールド固定順） |
| `EntityRegistry.RegisterWithId` | 登録順依存の根絶。マスタデータIDと揃えられる | 衝突は例外で即検知。サンプルは明示ID方式に移行済み |
| `IInputCodec<T>` + `InputJournalCodec` | リプレイを byte[] に保存/復元（版数チェックつき） | 実装例: `Sample_ActionInputCodec`。形式変更時は Version++ |
| `RecordLog.TryRead(ref cursor, out r)` | 演出Presenter・実績・テレメトリが独立カーソルで「新着だけ」読める | 消費者ごとに int カーソルを1つ持つだけ |
| `FactoryRegistry.Bind` 重複ガード | 二重バインド（構成ミス）を黙って上書きせず例外で検知 | 意図的な差し替えは `allowOverwrite: true` |
| 戦闘不能ガード（サンプル） | 死亡アクターの行動を発動判定が最終防衛として弾く | Fuzzテストのようなランダム入力にも安全 |
| Fuzzテスト | ランダム入力×リプレイ一致で決定性を常時監視 | `Tests/Editor/FuzzTests.cs`（生成も決定的＝失敗シードで完全再現） |
| Presentationブリッジ（v0.4） | レコード→演出の「橋」の定型部分を提供。コアの純度は不変 | 即時型=`RecordPresenterBase`（ActionBattle Runnerが実例）/ 順次再生型=`RecordPlaybackQueue`（CommandBattle Runnerが実例） |

## 4. 設計原則と反映マップ（レビュー内容の集大成）

| 原則（レビュー由来） | 実装 |
|---|---|
| 発火順の決定性 | `ILogicEventHandler.Priority` + EventHub の安定挿入。ゲームごとに優先度規約クラス必須（サンプルの `HandlerOrder`） |
| 再帰・連鎖の暴走対策 | セクション深度上限 / 1解決あたり発火数上限（`BeginResolution`）/ ハンドラー再入禁止 |
| GC対策 | 型別購読（is判別なし）/ `EventPool<T>` / 発火中解除は墓標方式 / struct入出力 / セクションはステートレス singleton / LINQ不使用 |
| float非決定性 | 全計算を整数‰（`Permille`）、時刻は long ms（`LogicContext.NowMs`） |
| 乱数の分離とシード入力 | `DeterministicRandom`（ロジック専用）。0‰/1000‰は乱数を消費しない規約。演出乱数はView層が別途持つ |
| internal×asmdef 問題 | 状態書き込みを Writer インターフェースに分離（サンプルの `IActorWriter`/`IUnitWriter`） |
| ログでなく事象レコード | `RecordLog<TRecord>`。文字列化はView層（サンプルの `Presenter`）で初めて行う |
| レコードの自己完結（教訓） | HP等は**スナップショットで焼き込む**。後から実体を読むと最終状態しか見えない |
| 所有者インデックス | `EventHub.UnsubscribeAll(owner)`（退場時の一括解除） |
| ハンドラーの状態レス化 | 可変状態は `ConditionSet`/アクター側へ。ハンドラーは不変設定のみ保持 |
| セクション入出力の標準化 | `Section<TInput,TResult>` + `ctx.RunSection`。結果は戻り値（可変プロパティ渡し禁止） |
| データ駆動 | 技→ハンドラーのバインド（`FactoryRegistry`）＝タイトルごとの差し替え点。ScriptableObject化は今後のTODO |
| ゴールデンログテスト | `Tests/Editor/SampleIntegrationTests.cs`（同一シード→同一レコード列） |

## 5. 使い方（新しいゲームを載せる手順）

1. **レコード型**（readonly struct）と **RecordKind** を定義し、`RecordLog<TRecord>` を `ctx.AddExtension` で登録
2. **優先度規約クラス**を定義（例: 武器=100→スキル=200→アイテム=300→敵状態=400）
3. **イベント**（sealed class : LogicEvent, Reset必須）を定義
4. **セクション**（Section<TInput,TResult>、ステートレス singleton）でロジックの流れを書く。個別ルールは書かず、要所でイベント発火
5. **ハンドラー**（LogicEventHandlerBase + ILogicEventHandler<T>）で個別仕様を書く。割り込み・連鎖は `ctx.RunSection` で
6. **合成ルート**で「このタイトルに存在する仕様」だけを `RegisterTo(ctx.Hub)`（未登録＝オミット）
7. **進行役**（ターンドライバー / アクション層）が入力を組み立て、`ctx.BeginResolution()` → セクション呼び出し
8. 出力された **レコード列をView層が解釈**（Debug.Log / UI / VFX / のけぞり再生）

両サンプルがこの手順の完全な実例になっている。まず `Sample_CommandBattle.cs` を上から読むこと。

## 6. サンプルの実行

- 空の GameObject に `Sample_CommandBattleRunner` または `Sample_ActionBattleRunner` をアタッチして Play → Console に事象レコードが流れる
- コマンドバトル: 晴れ補正 → ほのおのパンチ（やけど）→ せいでんき割り込み → クラボのみ連鎖 → まもる（発動判定で技失敗）
- アクションバトル: 鬼人薬 → 会心 → 爆破蓄積 → 爆破発動（連鎖）→ 部位破壊（連鎖の連鎖）→ ガード（チップ0）→ 鬼人薬失効 → 怒り → スタミナ不足
- **対話型サンプル**（ユーザー入力で遊ぶ版）:
  - `Sample_CommandBattleInteractiveRunner` … uGUIのButtonで技を選ぶターン制対戦（UIはコード生成。アタッチだけで動く）
  - `Sample_ActionBattleInteractiveRunner` … Input.GetKey操作のリアルタイム狩り（[1][2][3]攻撃 [4]鬼人薬 [G]ガード [V]リプレイ検証 [R]再戦）。
    あなたのプレイ自体が InputJournal に記録され、[V] で「入力列＋シードから完全再現できること」をその場で確認できる
  - ※ アクション側は旧Inputクラスを使うため Project Settings > Player > Active Input Handling を「Input Manager (Old)」か「Both」にすること
- **画面反映サンプル**（3D表示・Animator対応。いずれもアタッチだけで舞台をコード生成）:
  - `Sample_CommandBattleViewRunner` … レコード→キャラ演出/Animator反映（トリガー: Attack/Hit/Faint、ブール: Guarding）
  - `Sample_ActionBattle3DRunner` … 3D物理の当たり判定＋入力状態機械＋敵AI＋Animator反映の総合デモ（WASD/[1][3]/[4]/[G]/[R]）
- 各サンプルの読解手順・活かし方・拡張案は `Docs/SAMPLES_GUIDE.md` を参照
- テスト: Window > General > Test Runner (EditMode) から実行（Assets 配下の asmdef なので追加設定は不要）

## 7. 検証について（重要）

この環境では .NET/Unity コンパイラを実行できなかったため、以下で検証済み:
- tree-sitter による全 .cs の構文解析（エラー0）
- ロジック全体を Python へ忠実移植したロジック（整数演算・xorshift32 乱数とも同一アルゴリズム）で、両サンプルの全シナリオ・全乱数分岐・レコード列を確認
- シードは物語が最良になる値を探索して固定（コマンド=11: 初撃でやけど発動 / アクション=1041: 初撃のみ会心）

**Unityでの初回コンパイルと Test Runner 実行は未実施。引き継ぎ後、最初にこれを行うこと**（コンパイルエラーがあれば軽微なはず）。

## 8. 今後のTODO（Claude Code への申し送り）

- [ ] Unity でコンパイル & EditMode テスト実行（§7）※最優先
- [x] サンプルを EventScope へ移行（全セクションで使用中）
- [x] ID方式レコード（ActionBattle が EntityRegistry で実装。CommandBattle は対比用に参照方式を維持）
- [x] リプレイの結線（ActionBattle: InputJournal 記録→再生→StableHash 検証。Runner が結果を表示）
- [x] 巻き戻しデモ（CommandBattle: お試しターン→破棄。Sample_BattleSnapshot）
- [x] スタートアップ/実装ガイドの作成（Docs/）
- [x] リネーム（GameCore / Seed.Core）
- [x] EventHub 購読スナップショット（巻き戻しの完全化）
- [x] 構造化ハッシュ・明示ID登録・入力コーデック（リプレイ保存の完成）
- [x] レコード消費カーソル / Bind重複ガード / トレース上限 / 戦闘不能ガード / Fuzzテスト
- [x] Presentationブリッジ（RecordPresenterBase / RecordPlaybackQueue。Runner2種を実例に載せ替え）
- [ ] デモシーン（.unity）の追加。Runner を置くだけ
- [ ] **手動パッケージ化（ユーザーが実施予定）**: このフォルダを `Packages/<name>/` へ移し package.json を追加するだけで UPM 化できる構成にしてある（asmdefはそのまま使える。サンプルを任意インポートにするなら `Samples~` へリネーム + package.json の `samples` 配列）
- [ ] ScriptableObject によるデータ駆動化（`FactoryRegistry` が受け口。MonsterHunterBattleSample の `WeaponDefinition` が参考実装）
- [ ] レコード→演出パイプラインの本実装（ヒットストップ・ダメージ数字・のけぞり優先度解決）
- [ ] 通信対戦: 入力だけ送って両端で再計算する方式の検証。レコード列の `StableHash` 比較で desync 検出（※唯一、意図的に基盤へ入れていない領域）
- [ ] エディタ用トレースビューア（`BufferedCoreTrace` の内容と RecordLog をタイムライン表示）
- [x] プロジェクト本体との統合方針: 仲介基盤（Seed.Hub）経由のメッセージ駆動を採用。
      GameCore は Hub すら参照せず、翻訳は Seed.App の CoreHubBridge が担う
      （全体像: Assets/Script/ARCHITECTURE.md / 実例: Sample_HubIntegrationRunner）。DI（VContainer等）は引き続き未定
- [ ] ベンチマーク（1解決あたりのアロケーションが0であることを Profiler で確認）

実装済みになったもの（旧TODOから昇格）: EventScope / ID台帳 / スナップショット&巻き戻し土台 / 入力ジャーナル / 安定ハッシュ / 重ねがけポリシー / トレース窓口 / 常時消費Roll / Fork。

## 9. コーディング規約（この基盤内）

- **1クラス1ファイル**（ファイル名＝型名）。例外は「同名のジェネリック/非ジェネリック対」と「密結合のインターフェース対」のみ
- **役割別フォルダ**: 型は役割ごとのサブフォルダ（Core/Events/Sections/Records/Replay/State/Numerics/Diagnostics 等）へ配置する
- **1行1文**: 複数の文を1行に並べない。単文の if も必ず波括弧を使い、`if (…)` と `{` `}` は各行に分ける（Allmanスタイル）
- **サンプルを追加・変更したら `Docs/SAMPLES_GUIDE.md` を必ず更新**（読解手順・活かし方・拡張案）
- **ドキュメントコメント必須**: すべての型・メソッド・コンストラクタに `<summary>`、すべてのフィールド・プロパティ・enumメンバーにコメントを付ける
- 詳細な実装規約とパターン集は `Docs/IMPLEMENTATION_GUIDE.md`、導入手順は `Docs/STARTUP_GUIDE.md` を参照
- コア（Runtime/）に UnityEngine・ゲーム固有概念・文字列組み立てを持ち込まない
- イベント型は sealed。プール使用時は Reset で参照を必ず切る
- ハンドラーは状態レス（不変設定のみ）。可変状態はアクター側 ConditionSet 等へ
- 数値計算は整数‰のみ。float・System.Random・DateTime を使わない
- サンプルコードはファイル名・主要クラス名に `Sample_` プレフィックスを付け、冒頭にサンプルである旨のヘッダーコメントを書く
- ホットパス（Fire/RunSection配下）で LINQ・クロージャ・boxing を発生させない
