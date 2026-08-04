# GameCore 実装ガイド（パターン集）

実際にゲームを実装するときの規約と、機能ごとの使い方・アンチパターンをまとめる。
初見の人はまず `STARTUP_GUIDE.md` から。各パターンの実例はサンプル内で `★パターン` コメントを検索すると見つかる。

---

## 1. 設計の地図

```
進行役（ドライバー）      : 入力を組み立てる。ターン管理/コリジョン/AIはここ
  └→ コア＋ゲームロジック : セクションが流れを進め、イベントで個別仕様が介入
        └→ 事象レコード       : 結果の記録。View層がこれを解釈して画面を作る
```

基盤に**置かないもの**: 毎フレームの移動・物理・カメラ・アニメ・UI遷移・文字列組み立て・UnityEngine型。

## 2. 事象レコードの設計

- 最初にレコード（RecordKind＋readonly struct）から設計する。「起こりうること一覧」＝仕様書になる
- **自己完結の原則**: HPなどの変動値はスナップショットで焼き込む。View層が後から実体を読むと最終状態しか見えない
- 2方式から選ぶ:

| 方式 | 実例 | 向き |
|---|---|---|
| 参照方式（アクター参照を直接持つ） | CommandBattle | 小規模・保存不要。いちばん簡単 |
| ID方式（EntityRegistry の int を持つ） | ActionBattle | セーブ/リプレイ保存/通信/サーバ検証が必要なら必須 |

ID方式の手順: 合成ルートで登場物（アクター・部位・モーション等）を全部 `EntityRegistry.RegisterWithId`
（マスタデータのIDと揃える。登録順を変えても保存物がズレない）→ レコードにはIDを入れる →
表示時に `GetEntity<T>(id)` で引き直す。自動採番の `Register` は使い捨ての実験用と割り切る。

## 3. イベントの規約

- `sealed class ○○Event : LogicEvent`、ペイロードは public フィールド、`Reset()` で参照を必ず切る
- 命名は「どのセクションの何を補正するか」が分かるように（AttackPowerEvent / FinalDamageEvent）
- 貸し借りは必ず `EventScope<T>`（using 自動返却）。`EventPool` 直叩きは返し忘れの温床

```csharp
using (var scope = EventScope<FooEvent>.Rent())
{
    var ev = scope.Event;
    ev.Value = baseValue;
    ctx.Hub.Fire(ev, ctx);
    result = ev.Value; // スコープ内で読み取ってから抜ける
}
```

- 発火中に登録された購読者は**その発火では呼ばれない**（次から優先度順）。曖昧さ排除のための仕様
- イベント型の継承はしない（完全一致型でのみ配信される）

## 4. セクションの規約

- `Section<TInput, TResult>`＋ステートレス singleton（`public static readonly ○○ Instance`、privateコンストラクタ）
- 入出力は readonly struct。結果は戻り値で返す（可変プロパティ渡しは禁止）
- **個別仕様を書かない**。「晴れなら1.5倍」をセクションに書いた時点で負け。イベントを発火してハンドラーに任せる
- 粒度は「再利用の単位」。迷ったら大きめに作り、2箇所目の利用者が現れたら分割
- 失敗の返し方: ゲーム的に正常な失敗は `SectionResult` / `SectionResult<T>`（`if (result)` で判定可、
  失敗コードはゲーム側enumをintで）。例外（LogicException）は設計バグ専用
- 実行は必ず `ctx.RunSection`（深度ガード＋トレースのため）。`ctx.BeginResolution()` を進行役の入口で忘れない

## 5. ハンドラー（個別仕様）の規約

- 1仕様1クラス。`LogicEventHandlerBase` を継承し、`ILogicEventHandler<TEvent>` を実装
- `RegisterTo(EventHub)` に購読を明示（この仕様が何に反応するか一目で分かる）
- **登録＝存在 / 未登録＝オミット**。タイトル差分・装備差分はすべて合成ルートの登録で表現する
- 複数イベントに反応する仕様は複数インターフェース実装（例: Sample_MonsterRageHandler）
- **状態レス**: 持ってよいのは不変の設定値のみ。効果時間・蓄積は ConditionSet / アクター側へ
- 優先度規約クラス（Sample_HandlerOrder 参照）を必ず定義する。整数演算では掛け算の順番で結果が
  変わるため、**適用順はゲーム仕様**。企画と共有しコメントで根拠を残す
- 割り込み・連鎖: ハンドラーから `ctx.RunSection` を呼び返す。無限連鎖は深度・発火数・再入の
  3ガードが止めるが、再入スキップはサイレント → 調査には TraceListener（§10）

## 6. コンディション（バフ・状態異常）

- `ConditionSet<TKind>`＋`TimedCondition<TKind>`（失効時刻ms・効果量）。永続は long.MaxValue
- 重ねがけは `AddOrMerge(cond, ConditionMergePolicy.X, nowMs)` で仕様を明示:
  Stack（併存）/ Extend（時間延長）/ Overwrite（上書き）/ AddMagnitude（加算）/ Ignore（無視）
- 失効処理は進行役が `RemoveExpired` で明示的に行い、失効レコードを出す（Sample_ActionDriver 参照）
- 時間基準（ms）とターン基準を1タイトルで混ぜない。ターン制では「永続＋明示解除」か
  「NowMs をターン数として使う」のどちらかに統一

## 7. 数値と乱数の規約（決定性の心臓部）

- float / System.Random / DateTime / string.GetHashCode を**ロジックで使わない**
- 倍率は ‰ の整数（`Permille.Apply`）。切り捨て方向も含めて仕様
- 乱数は `ctx.LogicRandom` のみ。演出用が欲しければ `Fork()` で子ストリームを切り出して渡す
  （ロジック側のストリームに演出の抽選を混ぜた瞬間、リプレイと通信同期が壊れる）
- `Roll(p)`: 0‰/1000‰ では消費しない（消費数まで決定的）。確率が補正で 0/1000 を跨ぎうる仕様は
  `RollAlwaysConsume(p)` を使う。**仕様ごとにどちらを使うか固定**し、途中で変えない
- シードは入力。0 は内部で置換されるので避ける

## 8. 巻き戻し・先読みAI

手順（Sample_CommandBattleDemo の「お試しターン」と Sample_BattleSnapshot 参照）:

1. `ctx.CaptureCoreSnapshot()`（時刻＋乱数状態）
2. `ctx.Hub.CaptureSnapshot()`（購読状態。きのみ消費＝解除・まもる＝登録も巻き戻すため）
3. ゲーム側状態のコピー（HP・コンディションは `ConditionSet.CopyTo`、その他の可変値も）
4. `RecordLog.Count` を控える
5. お試し実行（乱数消費・購読変更が起きてもよい。全部巻き戻る）
6. 復元: `RestoreCoreSnapshot` → `Hub.RestoreSnapshot` → ゲーム側復元 → `RecordLog.TruncateTo`

可変状態がアクター側に一元化されていれば（§5）、コピー対象は少なくて済む。
購読スナップショットはリスト複製を伴うので、毎フレームではなく「節目」で使うこと。

## 9. リプレイ（Sample_ActionBattleDemo 参照）

- 入力を「ID参照の readonly struct」で定義する（オブジェクト参照を入れない）
- 「時間を進める」も入力として記録すると、再生は先頭から流し直すだけになる
- `InputJournal<TInput>` に Record（シード・版数もセットで保持）
- 検証は `StableHash` ×「構造化ハッシュ」: レコードに `IHashableRecord.AddTo` を実装し、
  表示文字列ではなくフィールドをハッシュ化する（文言修正で検証が壊れない）
- 一致しない＝決定性が壊れた（演出乱数の混入・登録順依存・floatの混入を疑う）
- 保存は `IInputCodec<TInput>` を実装して `InputJournalCodec.ToBytes / FromBytes`
  （magic・版数・シード込み。版数不一致は null＝再生拒否が既定）。実例: Sample_ActionInputCodec
- Fuzzテスト（Tests/Editor/FuzzTests.cs）が「ランダム入力でもリプレイが常に一致」を常時監視する

## 10. デバッグとトレース

- `ctx.TraceListener = new BufferedCoreTrace()`（開発時のみ）でセクション入退・イベント発火・
  再入スキップが観測できる。「連鎖が発動しない」の第一容疑者＝再入スキップはここに出る
- 事象レコード＝ゲームとして何が起きたか / トレース＝コアがどう動いたか、の役割分担
- 両ランナーの Inspector にトレースON/OFFのチェックボックスがある

## 11. データ駆動化

- `FactoryRegistry<TKey, TArg, TProduct>` が「キー→振る舞い生成」の受け口
  （例: 技→まもるハンドラー。タイトルごとにバインドを差し替えるだけで挙動が変わる）
- ScriptableObject 化するときは「SOを読む→FactoryRegistryへ流し込む」変換を合成ルートに置き、
  コア・セクション・ハンドラーはSOの存在を知らないままにする
- 効果の完全データ化（条件式評価など）はやりすぎ注意。オペレーションの語彙を絞る

## 12. テスト戦略

- **決定性テスト**: 同一シードで2回実行→レコード列一致（最重要。壊れたら§7違反を疑う）
- **物語テスト**: デモに期待する事象（連鎖・巻き戻し痕跡なし等）が含まれるかをRecordKindで確認
- **パラメタライズド**: 閾値・境界の表（[TestCase]）で連鎖の回帰を防ぐ
- **ゴールデンログ**: 代表シナリオのレコード列（またはStableHash値）をスナップショット保存し、
  差分が出たらレビュー対象にする
- コアはUnityEngine非依存なのでEditModeで全部回る。実例: Tests/Editor/

## 13. パフォーマンス規約

- 演出Presenterは自作せず Presenter/ の部品を使う:
  即時反映型は `RecordPresenterBase<TRecord>` を継承して Dispatch を実装（実例: Sample_ActionBattleRunner）、
  ターン制の順次再生は `RecordPlaybackQueue<TRecord>`＋`IPlaybackStep`（実例: Sample_CommandBattleRunner）。
  メタシステム（実績・テレメトリ）が生で読む場合は `RecordLog.TryRead(ref cursor, out r)`
  （消費者ごとに独立カーソル。「どこまで処理したか」を自前indexで管理しない）
- ホットパス（Fire / RunSection 配下）で LINQ・クロージャ・boxing・文字列組み立てを発生させない
- イベントは EventScope（プール）。レコードは struct を RecordLog に積む（アロケーションなし）
- セクションは singleton（実行ごとの new なし）
- `GetExtension` はセクション冒頭で1回だけ引いてローカルに持つ
- ソートが必要なら挿入ソート等を手書き（Sample_TurnDriver 参照）

## 14. アンチパターン集

| やりがち | なぜダメか | 正解 |
|---|---|---|
| セクション内に「晴れなら1.5倍」 | 個別仕様がロジックに漏れて講演以前の状態に戻る | イベント発火＋ハンドラー |
| ハンドラーに残り時間フィールド | セーブ・巻き戻し・同期から漏れる | ConditionSet へ（状態レス） |
| ctx.LogicRandom で演出の抽選 | 乱数列がズレて対戦結果が変わる | Fork した子を演出に渡す |
| 登録順で補正順を制御 | 並び替え1つで数値が変わる隠れ仕様になる | 優先度規約クラス |
| レコードから actor.Hp を表示 | 最終状態しか見えない | スナップショットを焼き込む |
| float でダメージ倍率 | 環境差で結果が揺れる | Permille |
| 例外でゲーム的失敗を表現 | 正常系が例外処理になる | SectionResult |
| EventPool を手で Rent/Return | 返し忘れ・二重返却 | EventScope |
| 表示文字列をハッシュ化して検証 | 文言修正で検証NGになる | IHashableRecord（構造化ハッシュ） |
| 自動採番IDのままリプレイ保存 | 登録順変更で保存物が壊れる | RegisterWithId で明示ID |
| 巻き戻し区間で購読変更を放置 | 解除/登録だけ元に戻らない | SubscriptionSnapshot も保存する |

## 15. サンプル→パターン対応表

| パターン | 実例の場所 |
|---|---|
| EventScope | 全セクション（両サンプル） |
| SectionResult | Sample_ActivationCheckSection（両サンプル） |
| 参照方式レコード | CommandBattle/Sample_Record |
| ID方式レコード＋EntityRegistry | ActionBattle/Sample_Record, Sample_ActionWorld |
| InputJournal＋StableHash リプレイ | Sample_ActionBattleDemo.VerifyReplay |
| CoreSnapshot 巻き戻し | Sample_CommandBattleDemo（お試しターン）, Sample_BattleSnapshot |
| ConditionMergePolicy | Ignore=Sample_StatusInflictSection / Extend=Sample_ActionDriver.UseDemonDrug / Overwrite=Sample_MonsterRageHandler |
| FactoryRegistry | Sample_CommandContext.Bindings（まもるのバインド） |
| 割り込み・連鎖 | Sample_StaticAbilityHandler / Sample_StatusBuildupSection→Sample_BlastExplosionSection |
| 複数イベント購読 | Sample_SunnyWeatherHandler / Sample_MonsterRageHandler |
| 状態レスハンドラー | Sample_DemonDrugHandler |
| 消費＝購読解除 | Sample_CheriBerryHandler |
| ターン限定効果の自己解除 | Sample_ProtectTurnHandler |
| 優先度規約 | Sample_HandlerOrder（両サンプル） |
| トレース | 両Runnerの Enable Kernel Trace |
| 書き込み口の分離 | Sample_IActorWriter / Sample_IUnitWriter |
| 購読スナップショット（巻き戻し完全化） | Sample_BattleSnapshot（CommandBattle） |
| 構造化ハッシュ | ActionBattle/Sample_Record.AddTo, Sample_ActionBattleDemo.HashRecords |
| 明示ID登録 | Sample_ActionWorld（RegisterWithId） |
| 合成ルートの分離（World） | Sample_CommandWorld / Sample_ActionWorld（世界の組み立てをデモ本体から分離） |
| 進行台本の分離（Scenario） | Sample_ActionBattleScenario（入力列の構築をフェーズ関数へ分割） |
| 対話型UI（入力待ち→解決→再生の状態機械） | Sample_CommandBattleInteractiveRunner（uGUI Button） |
| 対話プレイの丸ごと記録とリプレイ検証 | Sample_ActionBattleInteractiveRunner（Input.GetKey + InputJournal） |
| レコード→Animator/キャラ演出の反映 | Shared/Sample_SimpleCharacterView + Sample_CommandBattleViewRunner |
| 3D当たり判定（有効フレーム・多段防止・部位特定） | View3D/Sample_HitboxTrigger3D + Sample_PartMarker3D |
| 入力による状態変化（プレイヤー状態機械） | View3D/Sample_PlayerController3D |
| 敵AIの状態機械（予兆→攻撃→隙） | View3D/Sample_MonsterController3D |
| 入力コーデック（リプレイ保存） | Sample_ActionInputCodec + SnapshotReplayTests |
| レコード消費カーソル | RecordLog.TryRead（§13 Presenterの推奨API） |
| 戦闘不能ガード | 両サンプルの Sample_ActivationCheckSection 冒頭 |
| Presenter（即時Dispatch型） | Sample_ActionBattleRunner : RecordPresenterBase |
| 順次再生キュー（ターン制演出） | Sample_CommandBattleRunner + RecordPlaybackQueue / TimedStep |
