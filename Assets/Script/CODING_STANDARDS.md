# Seed コード規約（CODING STANDARDS）

本プロジェクトのコードを書く・レビューするときの規約集です。
**規約には必ず「なぜ」を添えます**——理由ごと理解していれば、規約が想定しない場面でも
同じ精神で判断できるからです。設計の全体図は [ARCHITECTURE.md](ARCHITECTURE.md)、
各基盤の使い方は [Docs/00_Roadmap.md](Docs/00_Roadmap.md) を参照してください。

---

## 1. 言語とコメント

| 規約 | なぜ |
|---|---|
| 会話・コメント・ドキュメント・コミットメッセージは**日本語** | チームの母語で書くのが最も誤解が少ない |
| **全メンバー**（public/private/フィールド/定数/enum値を問わず）に `///` docコメント | 「読めば分かる」は書いた本人の錯覚。3ヶ月後の自分は他人 |
| docコメントには「何を」だけでなく**「なぜ」**を書く | 「何を」はコードが語る。消えるのは「なぜ」の方 |
| コードのコメントは**関数・クラスの役割を説明するサマリーのみ** | 変更点・作業報告・経緯の注釈はコードに残さず、会話で直接報告する。混ざると第三者に不要な確認を生む |
| **作業報告を成果物（コード・md）へ書かない** | 「〜しました」「旧〜」等の報告文はレビュー時のゴミになる |
| 例外: **`Sample_` ファイルのみ学習用の説明コメントを許可** | サンプルは読んで学ぶ教材（それでも作業報告は書かない） |

```csharp
/// <summary>重力（m/s²。実物理より強めがゲームの定番＝落下のキレを出す）。</summary>
public float Gravity { get; set; } = -22f;
```

## 2. 命名

| 対象 | 規約 | 例 |
|---|---|---|
| 型・メソッド・プロパティ・定数 | PascalCase | `AnimationDriver` / `TryStepUp` / `StepOffset` |
| private フィールド | `_camelCase` | `_playerMotor` / `_deltaTime` |
| 名前空間・asmdef | `Seed.<領域>`（基盤） / `Seed.App`（アプリ） | `Seed.Motion` / `Seed.App` |
| サンプルコード | **`Sample_` プレフィックス**・1クラス1ファイル | `Sample_BattlePhase` |
| タイトル固有の語彙 | `Game.<Title>.Contracts` に置く | `Game.Battle.Contracts` |
| bool | 疑問形（`Is`/`Has`/`Can`/`Was`） | `IsGrounded` / `HasTarget` |
| メソッド | 動詞始まり。失敗しうる取得は `Try〜` + bool戻り | `TryStart` / `TryGet` |

- **なぜ `Sample_` か**: 基盤コードと「消してよいデモコード」を検索一発で区別するため。
- **なぜ 1クラス1ファイルか**: Unity の .meta / GUID 管理とレビューの単位を揃えるため。

## 3. 設計原則（家訓）

コードレビューで最も重く見る5箇条です。

1. **状態は Tick、艶は Update** — ゲームの真実（位置・HP・行動状態）は `TickPipeline` が
   刻み、見た目の味付け（IK・揺れもの・カメラ・Tween）は `Update`/`LateUpdate` が重ねる。
   *なぜ*: 真実と演出を分ければ、リプレイ・ヘッドレステスト・スローモーションが壊れない。
2. **方針は App、仕組みは基盤** — 「どのクリップを流すか」「どの距離で拾えるか」は App が決め、
   基盤は「流す仕組み」「解決する仕組み」だけを提供する。
   *なぜ*: タイトルごとの調整が基盤の再コンパイルなしに済む。
3. **命令の処理者は 1 基盤** — Hub の命令（`〜Command`）を処理するのは必ず1つの基盤。
   通知（過去形イベント）は誰が何人購読してもよい。
   *なぜ*: 命令の処理者が2人いると実行順・二重処理のバグが生まれる。
4. **真実は一箇所** — 位置の真実は `ActorPose`、行動遷移の真実は Behavior 状態機械。
   Transform や Animator は「写し先」であって真実ではない。
   *なぜ*: 真実が二重化した瞬間、同期バグが無限に湧く（AnimatorController を使わないのも同じ理由）。
5. **純C#コア・MonoBehaviour は端** — ロジックは UnityEngine 非依存の純C#で書き、
   MonoBehaviour は入出力の端（表示・入力・物理）にだけ置く。
   *なぜ*: EditMode テストで数百件を数十秒で回せる。決定性も守れる。

## 4. ID・データの扱い

- ID は **int を包む値型 struct**（`MotionClipId` / `ViewpointId` / `ActionId` など）。
  *なぜ*: enum は増やすたび基盤に手が入り、生 int は取り違える。値型 struct は
  「基盤は番号しか知らない・意味づけは App」の分業を型で強制できる。
- 予約番号帯を決めてから増やす（例: モーション番号 1〜99=行動キー同値、100〜=App 独自）。
- マスターデータは `MasterMemory`（[07_Data](Docs/07_Data.md)）。ScriptableObject は
  エディタ入力の受け口までとし、実行時の真実はベイク済みバイナリに置く。

## 5. 依存規則

**外部ライブラリはどこからでも使えるわけではありません。**

| アセンブリ | 許可される外部依存 |
|---|---|
| `Seed.Core` / `Seed.Hub.Contracts` / `Seed.Persistence` | **なし**（純C#のみ） |
| `Seed.Flow` | UniTask |
| `Seed.Assets` | UniTask + Addressables |
| その他の基盤（Motion / Character / Cameras / …） | UnityEngine のみ |
| `Seed.App`（合成ルート・サンプル） | VContainer / LitMotion / MasterMemory / ZLogger / UnityDebugSheet など全て |

*なぜ*: 基盤を他プロジェクトへ持ち出すとき、外部依存が根に食い込んでいると摘出できなくなる。
「豪華な道具は端（App）で使う」が原則です。詳細は [18_Libraries](Docs/18_Libraries.md)。

## 6. Unity 実装規約

### 移動・物理

- **`CharacterController` は使わない**（サンプル含む）。移動体は
  **CapsuleCollider ＋ kinematic Rigidbody ＋ 自前 collide-and-slide**
  （実例: `Sample_KinematicMotor`）。
  *なぜ*: CC はブラックボックス（接地判定のちらつき・重力なし・調整不能）で、
  品質問題の原因を特定できない。自前ならテストで挙動を固定できる。
- 物理クエリは **NonAlloc 系＋使い回しバッファ**（毎フレームのGCゼロ）。
- クエリの**自己除外はレイヤーではなく `attachedRigidbody` 比較**で行う。
  *なぜ*: レイヤー除外はキャラ同士の衝突まで消してしまう（非対称衝突の温床）。
- 本プロジェクトは `AutoSyncTransforms = 0`。**Transform をコードで一括移動したら
  `Physics.SyncTransforms()` を呼ぶ**（実例: `OriginShiftSystem.Apply`）。
  *なぜ*: 呼ばないと次の物理ステップまでクエリが旧座標の世界を見る。

### 表示・アニメーション

- **AnimatorController アセットは使わない**。再生は Playables 直駆動（`AnimationDriver`）。
  *なぜ*: 遷移の真実は Behavior 状態機械にある（§3-4）。状態機械の二重化を避ける。
- `AnimationDriver.Play` は**非ループクリップを毎回先頭から流し直す**（攻撃連打用の仕様）。
  毎フレーム呼ぶ場所では `CurrentMotion` を確認してから呼ぶ。
  *なぜ*: 確認せずに毎フレーム呼ぶと、最初のポーズで凍りつく。
- レンダラーの色替えは `renderer.material` 直接代入**禁止**、
  `MaterialPropertyBlock`（`RendererTint`）を使う。
  *なぜ*: `.material` は暗黙にマテリアルを複製し、メモリとバッチングを蝕む。
- IK・揺れものは `IPoseRig` として `MotionRig`（LateUpdate）へ `With()` で装着する。
  リグへ渡す目標・重みの**時間平滑は配線側（App）の責務**（リグは即時反映）。

### レイヤー・ライフサイクル

- プレイヤーの体は **Ignore Raycast（layer 2）**——足IK・カメラのレイが自分に当たらないため。
  モーターのクエリはこのレイヤーも対象に含める（§自己除外は Rigidbody 比較）。
- フェーズ（`GamePhase`）で作ったものはフェーズで片付ける——`OnExit` で参照を null に戻し、
  実行時生成の Material 等は明示的に `Destroy`。
  *なぜ*: ステージ切替のたびに漏れが積もる。「作った者が片付ける」で追跡可能に保つ。
- 新規アセット・スクリプトを追加したら **.meta ごとコミット**する
  （エディタを一度開くか、バッチ起動で生成してから）。
  *なぜ*: .meta が無いと他環境で GUID が振り直され、参照が壊れる。

## 7. 非同期・Tween・ログ

- 非同期は **UniTask**（コルーチン禁止）。フェーズ遷移・フェードは
  `UniTaskFlowOperation` の流儀に合わせる。
- UI・演出の補間は **LitMotion をコードから**制御する（インスペクターの Tween 設定に依存しない）。
  *なぜ*: 演出のパラメータもコードにあれば、レビュー・検索・一括調整ができる。
- ログは `GameLog`（ZLogger）。`Debug.Log` はサンプルの一時的な確認までに留め、
  接頭辞（`[Pickup]` など）で発生源を明示する。

## 8. エラー処理と防御

- **プログラミングエラーは即例外**（例: ObjectPool の二重返却）。
  *なぜ*: 静かに壊れるより現場で即死する方が、原因のスタックが残る。
- **環境起因の欠落は静かにフォールバック**（例: モデル未取得なら null を返し、
  呼び出し側がカプセル表示へ落とす）。
  *なぜ*: アセットの有無で CI やチームメンバーの起動を止めない。
- 境界値・重み等、外へ渡す値は**契約された範囲**（0〜1 など）に必ずクランプする。

## 9. テスト

- 新しいロジックには **EditMode テスト**を付ける（純C#設計なら自然に書ける）。
- テスト名は**日本語で仕様を語る**: `段差を登れる` / `走り出すと中断してアイテムは残る`。
  *なぜ*: テスト一覧がそのまま仕様書になる。
- `[TearDown]` で作った GameObject を必ず破棄する（`DestroyImmediate`）。
- 決定性を検証できるものは検証する（同じ入力列 → 同じ結果。SpringBone・StageGen が実例）。
- 物理クエリを使うテストはコライダー生成後に `Physics.SyncTransforms()` を呼ぶ。
- 実行はエディタの Test Runner、または CLI:

```bash
/Applications/Unity/Hub/Editor/6000.5.5f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -projectPath <プロジェクトパス> -runTests -testPlatform EditMode -testResults /tmp/results.xml -logFile /tmp/tests.log
```

## 10. ドキュメント（Markdown）

- 山括弧を含む語は**必ずバッククォート**で囲む: `Game.<Title>.Contracts`。
  *なぜ*: 生の `<Title>` は HTML タグと誤認されて表示が欠ける。
- `Docs/` の章番号は**学習順**。新章は既存章の理解だけで読めるよう、依存する章の後ろに置く。
- 専門用語の初出には注釈ブロック（`> 📖 **用語 — 〜**:`）を付ける。
- 例え話は控えめに。長くなっても**具体的・正確**を優先する。

## 11. Git 運用

- ブランチ: `feature/<領域>/<内容>`（例: `feature/base-environment/3d-sample-fix`）。
- **コミット・プッシュは人間が行う**（AI アシスタントはステージまで）。
- コミット前チェック: ① EditMode テスト全緑 ② 新規ファイルの .meta 同梱
  ③ ドキュメント（該当章・ARCHITECTURE.md）の追随。
- 未コミットの作業ツリーがある状態でのブランチ切替・pull は消失の元。
  切替前に必ずコミットか stash を行う。

---

## 迷ったら

1. その真実は誰が持つべきか（§3-4）を先に決める
2. 方針（数値・選択）を App へ、仕組みを基盤へ置けているか（§3-2）を確認する
3. テストで挙動を固定できる形か（§9）を考える——できないなら設計を疑う

規約への追加・変更はこのファイルへ**理由ごと**追記してください。
