# サンプル読解ガイド（SAMPLES_GUIDE）

各サンプルの「目的・読み進め方・処理の流れ・ゲーム開発への活かし方・拡張案」をまとめる。
**サンプルを追加・変更したら、必ず本ファイルも更新すること**（DESIGN_NOTES §9 の規約）。

読む前提: `STARTUP_GUIDE.md` で基盤の5つの登場人物（LogicContext / Section / イベント＋ハンドラー / RecordLog / 支援部品）を把握していること。

---

## 全体マップ（どれから読むか）

| # | サンプル | 種類 | 主に学べること |
|---|---|---|---|
| 1 | Sample_CommandBattleRunner | 自動・ログ | 基盤の基本形。順次再生キュー |
| 2 | Sample_ActionBattleRunner | 自動・ログ | ID方式レコード・リプレイ検証 |
| 3 | Sample_CommandBattleInteractiveRunner | 対話・uGUI | 入力待ち→解決→再生の状態機械 |
| 4 | Sample_ActionBattleInteractiveRunner | 対話・キー入力 | 対話プレイの丸ごと記録と検証 |
| 5 | Sample_CommandBattleViewRunner | 自動・3D表示 | レコード→キャラ演出/Animator反映 |
| 6 | Sample_ActionBattle3DRunner | 対話・3D物理 | 当たり判定・状態機械・敵AI・総合 |
| 7 | Sample_HubIntegrationRunner（App） | 対話・3D表示 | 3基盤+GameCoreの仲介基盤（Hub）連携 |

推奨順は 1 → 2 → 3 → 5 → 4 → 6。1・2で「ロジックは一瞬で確定し、演出が追いかける」を掴み、
3で入力、5で画面反映、4で決定性、6で全部盛りを確認する流れが最短。

---

## 1. Sample_CommandBattleRunner（自動・ログ出力）

**目的**: 基盤の最小の使い方。ターン制の1試合を自動実行し、事象レコードを時間差で表示する。

**読み進め方**:
1. `CommandBattle/Presenter/Sample_CommandBattleRunner.cs` … 入口。RunDemo→キュー再生の骨格
2. `CommandBattle/Demo/Sample_CommandBattleDemo.cs` … 3フェーズ（開幕→お試し巻き戻し→まもる）
3. `CommandBattle/Driver/Sample_CommandWorld.cs` … 合成ルート（登録＝存在の現場）
4. `CommandBattle/Sections/Sample_MoveEffectSection.cs` … 技の流れの定義
5. `CommandBattle/Handlers/` の4つ … 個別仕様（晴れ/せいでんき/きのみ/まもる）

**処理の流れ**: Start で試合全体が一瞬で確定 → レコードを `RecordPlaybackQueue` へ →
Update の Tick が0.4秒ごとに1行ずつ表示。Inspector の再生速度を変えても結果が不変な点が肝。

**活かし方**: 新しいターン制ゲームの雛形。まず World を写経して自分の駒・技に差し替える。

**拡張案**: 3人以上の同時ターン／行動順の速度比較を氷や麻痺で動的化／お試しターンをAI思考に転用。

## 2. Sample_ActionBattleRunner（自動・ログ出力）

**目的**: リアルタイム制でも同じ基盤で動くこと、ID方式レコード、リプレイ検証の実演。

**読み進め方**:
1. `ActionBattle/Presenter/Sample_ActionBattleRunner.cs` … RecordPresenterBase 継承の実例
2. `ActionBattle/Demo/Sample_ActionBattleDemo.cs` … 入口とリプレイ検証（VerifyReplay）
3. `ActionBattle/Demo/Sample_ActionBattleScenario.cs` … 進行台本（フェーズ関数分割の見本）
4. `ActionBattle/Driver/Sample_ActionWorld.cs` … 明示ID登録（RegisterWithId）
5. `ActionBattle/Sections/Sample_HitResolutionSection.cs` … ヒット解決8段パイプライン
6. `ActionBattle/Replay/` … 入力struct・コーデック（リプレイ保存）

**処理の流れ**: 入力列を「記録してから実行」→ 最後に別世界で再実行してハッシュ照合→「リプレイ検証 OK」。

**活かし方**: リプレイ・サーバ検証・オートテストが必要なゲームの土台。レコード設計の手本。

**拡張案**: journalのファイル保存/読込（InputJournalCodec使用）／別バージョン検出の実運用化。

## 3. Sample_CommandBattleInteractiveRunner（対話・uGUI Button）

**目的**: ユーザー入力で進むターン制の正しい構成＝「入力待ち→解決（即時）→再生→入力待ち」。

**読み進め方**: このファイル単体で完結。`Phase` enum → `SubmitPlayerAction` → `OnPlaybackFinished`
の順で読むと状態機械が追える。UI生成（BuildUi以下）は後回しでよい。

**処理の流れ**: Buttonクリック→敵AIの手を決めて `RunTurn`（結果確定）→新着レコードをキューへ→
再生中はButton無効→再生完了で勝敗判定か次の入力待ちへ。

**活かし方**: コマンドRPG/SRPGのバトル画面の骨格そのもの。Buttonを本物のUIに、
AppendLog をアニメ再生に置き換えるだけで実戦形になる。

**拡張案**: 技を4つに増やしPP管理／先行入力バッファ／敵AIをユーティリティ式に／
プレイヤー入力を InputJournal に記録して「昨日の対戦を観る」。

## 4. Sample_ActionBattleInteractiveRunner（対話・Input.GetKey）

**目的**: リアルタイム操作でも決定性が保たれることの体感。「あなたのプレイ」がそのまま入力列になる。

**読み進め方**: `Update` → `HandlePlayerInput` → `Execute`（記録してから実行）→ `VerifyReplay` の順。
`AdvanceLogicTime` の端数繰り越し（ミリ秒未満を捨てない）も見どころ。

**処理の流れ**: 毎フレーム時間経過も入力として記録。[V]で入力列＋シードから再計算しハッシュ照合。

**活かし方**: 「操作を全部ログに残す」設計の実例。バグ報告に入力列を添付する運用や、
チート検証（サーバで同じ入力を再計算）の原型。

**拡張案**: journalの自動保存とタイトル画面からの再生（観戦モード）／
操作リマップ／ガードをタイミング判定（ジャスガ）にして入力へ精度を記録。

## 5. Sample_CommandBattleViewRunner（自動・3D表示＋Animator反映）

**目的**: レコード→キャラクター演出の翻訳。Animatorへの反映パターン。

**読み進め方**:
1. `Shared/Sample_SimpleCharacterView.cs` … 意味API（PlayAttack/PlayHit/SetGuarding/PlayFaint）。
   Animator があれば SetTrigger/SetBool、無ければ Transform 簡易演出、の二段構え
2. `CommandBattle/View/Sample_CommandBattleViewRunner.cs` … `ApplyRecord` のswitchが
   「レコード→演出の対応表」の実物

**処理の流れ**: 3ターンの筋書きを一瞬で解決→キューが1件ずつ `ApplyRecord`→
ActionDeclared=踏み込み / Hit=のけぞり＋HP0なら倒れ / Guarding=構え表示。

**活かし方**: 本物のAnimatorControllerを作り、生成されたカプセルのViewに割り当てるだけで
トリガー駆動を確認できる（パラメータ名: Attack / Hit / Faint / Guarding）。
実プロジェクトでは View の中身だけを差し替え、`ApplyRecord` の対応表は温存する。

**拡張案**: ダメージ数字のワールド空間ポップ／カメラを話者に寄せる（レコード→Cinemachine）／
Timelineでカットイン（AbilityTriggered連動）。

## 6. Sample_ActionBattle3DRunner（対話・3D物理・総合）

**目的**: 3D当たり判定・入力状態機械・敵AI・Animator反映・レコード反映の総合実演。
実装ガイド§1「フレームの世界と解決の世界」の完全なコード化。

**読み進め方**（この順が重要）:
1. `View3D/Sample_ActionBattle3DRunner.cs` … 舞台生成と配線だけ。各部品の関係を掴む
2. `View3D/Sample_PlayerController3D.cs` … ★状態機械（Idle/Attacking/Guarding/Dead）。
   攻撃開始時に発動判定セクションへ問い合わせ、有効フレームでヒットボックスをON/OFF
3. `View3D/Sample_HitboxTrigger3D.cs` … ★当たり判定。多段ヒット防止もここ（アクション層の責務）
4. `View3D/Sample_PartMarker3D.cs` … コライダー⇔ロジック部位の対応づけ
5. `View3D/Sample_MonsterController3D.cs` … ★敵AI（Cooldown→予兆→攻撃）。
   射程判定とガード判定をAIが行い、結果だけを HitRequest でロジックへ
6. Dispatch（Runner内）… レコード→被弾演出の翻訳

**処理の流れ（攻撃1回）**:
[1]キー → 発動判定（スタミナ。失敗ならレコードで通知され状態は変わらない）→ Attacking状態
→ 0.15秒後ヒットボックスON → 頭/翼コライダーに接触 → PartMarkerで部位特定
→ HitResolutionSection（肉質・会心・爆破蓄積…すべてロジック）→ レコード → 被弾演出。

**活かし方**: 3Dアクションの垂直スライス雛形。CharacterController・root motion・
Cinemachineに置き換えても、「発動判定→状態遷移→接触→HitRequest」の並びは変えない。

**拡張案**: 回避ステップ（無敵時間はアクション層／スタミナ消費は発動判定）／
複数部位の破壊で肉質変化を可視化（PartBrokenレコード→部位の色替え）／
敵AIに距離詰め移動と技の使い分け／被弾方向によるのけぞり分岐（レコードに方向を焼き込む）。

---

## 7. Sample_HubIntegrationRunner（対話・基盤統合 / Assets/Script/App/Samples）

**目的**: Hub / Character / UI / GameCore の4者が「互いを知らないまま」1本のゲームループとして回ることの実証。
アーキテクチャ全体像は `Assets/Script/ARCHITECTURE.md` を先に読むこと。

**読み進め方**:
1. `App/Samples/Sample_HubIntegrationRunner.cs` … 合成ルート。Startの番号付き手順と、Updateの実行順制御
2. `App/CoreHubBridge.cs` … GameCore⇔Hubの翻訳表（レコード→通知 / 要求→セクション実行）
3. `App/Samples/Sample_ReactionPolicy.cs` … 「被弾したらのけぞる」という方針の置き場
4. `Hub/Runtime/MessageHub.cs` … 配達の仕組み（優先度・発行中の解除/登録の安全化）
5. `App/Tests/Editor/HubIntegrationTests.cs` … MonoBehaviourなしで往復を検証する統合テスト

**処理の流れ**: [1]キー → AttackRequested → Bridge → GameCore解決 → レコード → Drain →
CharacterDamaged → 方針 → PlayReactionCommand → キャラのけぞり ＆ HUDのHP更新。
死亡時は CharacterDied → 方針 → ShowScreenCommand(Result) → UIのRouterが画面切替。

**活かし方**: 新しい基盤（サウンド・セーブ・実績など）を足すときの雛形。
「Hubだけを参照する基盤を作る → 契約に境界を越える型を足す → Appで配線」の3手順で増やせる。

**拡張案**: カメラ基盤の追加（CharacterDamaged→Impulse。ICharacterQueryで追従位置を読む）／
方針のタイトル差し替え（スーパーアーマー実装）／GuardInputChangedのリプレイ記録への統合。

## サンプル追加時のチェックリスト（保守ルール）

1. `Sample_` プレフィックス・1クラス1ファイル・役割フォルダ・全メンバーdoc（DESIGN_NOTES §9）
2. ロジックに手を入れたら決定性テスト（同一シード2回実行）を確認
3. 本ファイルに「目的／読み進め方／処理の流れ／活かし方／拡張案」を追記
4. DESIGN_NOTES §6 の一覧と IMPLEMENTATION_GUIDE §15 の対応表を更新
