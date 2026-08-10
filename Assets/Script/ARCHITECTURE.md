# Seed アーキテクチャ全体図（基盤の地図）

> **サンプルの起動方法・各基盤の使い方は [Docs/00_Roadmap.md](Docs/00_Roadmap.md)（スタートアップガイド集）へ。**
> **コードの書き方・設計原則の規約は [CODING_STANDARDS.md](CODING_STANDARDS.md) へ。**

プロジェクト全体の基盤構成と依存ルール。各基盤の詳細は各フォルダのガイド（`Docs/` および `GameCore/Docs/`）を参照。

## 基盤一覧と依存ルール

Assets/Script/
├─ GameCore/    Seed.Core（決定的ロジック）/ .Presenter / .Samples   … 何にも依存しない
├─ Hub/         Seed.Hub（MessageHub/ServiceRegistry/SubscriptionBag）
│               + Seed.Hub.Contracts（エンジン恒久契約。noEngineReferences）
│               + Seed.Hub.Unity（HubVector3⇔Vector3 変換だけの薄い橋）      … 何にも依存しない
│               + Seed.Hub.Editor（メニュー Seed/Message Tracer＝メッセージフローの観測ウィンドウ）
├─ Game/        Game..Contracts（タイトル/ジャンル固有の契約。例: Game.Battle.Contracts）
│               … Seed.Hub.Contracts のみに依存。タイトル追加＝契約asmdef追加で、恒久契約は肥大しない
├─ Character/   Seed.Character（アクター制御。下記参照）              … Hubのみに依存
├─ UI/          Seed.UI（レイヤ付き画面交通整理: UIScreen/ScreenRouter）… Hubのみに依存
├─ Input/       Seed.Input（InputSnapshot/InputRouter/InputSystemReader）… Hubのみに依存
├─ Flow/        Seed.Flow（GamePhase/GameFlow/ISceneLoader。フェーズ＝ホーム/戦闘/ショップ等の切替）… Hubのみに依存
├─ Clock/       Seed.Clock（GameClock。ポーズ/倍速/ヒットストップ＝dtの供給源）… Hubのみに依存・純C#
├─ AI/          Seed.AI（AiBrain/Consideration/AiDirector/InputEmulator。キャラAI＋メタAI）
│               … Hub と Seed.Character に依存（意図＝CharacterIntent の語彙の上に構築）
├─ Persistence/ Seed.Persistence（FileSaveStore/SaveEnvelope。byte[]の安全な永続化）… Hubのみに依存・純C#
├─ Motion/      Seed.Motion（Playables直駆動のアニメ再生・姿勢/IK/揺れものリグ・‰イベント）
│               … Seed.Characterに依存（艶レイヤー。AnimatorControllerアセット不要）
├─ Cameras/     Seed.Cameras（視点IDでの切替・重ね合わせ。Cinemachine 3.1.7 を駆動）
├─ Pooling/     Seed.Pooling（オブジェクトプール。二重返却は即例外・統計つき。依存なし）
├─ World/       Seed.World（原点回帰。float精度の維持。判断=純C#/適用=MonoBehaviour）
├─ Logging/     Seed.Logging（ZLogger のゼロアロケ構造化ログ。GameLog 窓口）
├─ AssetLoad/   Seed.Assets（IAssetLoader 契約＋Addressables 実装。UniTask で await）
├─ StageGen/    Seed.StageGen（設計図生成パイプライン＋施工。迷路/街/テンプレ/バイオーム/配置）
│               … Seed.Coreのみに依存（DeterministicRandom）。生成コアは純C#・施工だけUnity
│               + Seed.StageGen.Editor（メニュー Seed/Stage Palette＝プレハブ↔役割の紐付けと検証）
├─ Cameras/     Seed.Cameras（視点IDでカメラを指名。FPS/TPS切替＋演出カメラの重ね）
│               … Hub と Unity.Cinemachine に依存（見え方はCinemachine、指名の契約だけをSeedが持つ）
├─ Pooling/     Seed.Pooling（ObjectPool/GameObjectPool/PoolRegistry。使い回しと統計）… 何にも依存しない
├─ World/       Seed.World（原点回帰。広大フィールドでのfloat精度の維持）… Hubのみに依存
├─ Data/        Seed.Data（マスターデータ→EntityRegistry/FactoryRegistry のローダ）… Seed.Coreのみに依存
│               + Seed.Data.Editor（メニュー Seed/Master Data Browser＝一覧・検索・ID重複検証・空きID提案。
│                 定義アセットの Inspector にも重複警告と空きID割り当てボタンが出る）
└─ App/         Seed.App（合成ルート・方針・CoreHubBridge・統合デモ）  … 全部を知る唯一の場所
App/Foundation/ = 合成ルートの再利用骨格
（TickPipeline / CompositionScope / RecordHubTranslator / LogicInputFunnel）


- **基盤同士は互いを知らない**。会話はすべて Hub（メッセージ）か ServiceRegistry（同期問い合わせ）経由
- **契約は2階層**。Seed.Hub.Contracts＝エンジン恒久契約（共有ID・恒久メッセージ・サービスIF）。
  タイトル固有の語彙（AttackRequested 等）は `Game.<Title>.Contracts` に置く。
  1基盤しか使わない型を契約に置いたら設計ミス
- **GameCore は Hub すら参照しない**（決定性・テスト・ヘッドレス実行の純度維持）。翻訳は App の CoreHubBridge が担う
- **方針（出来事→リアクションの変換ルール・AI思考・ターゲット選択）は App が持つ**。基盤は無方針の道具に徹する
- asmdef名は将来のUnityパッケージ名規約。分離時はフォルダを Packages/ へ移して package.json を足すだけ

## 通信の使い分け

| したいこと | 手段 | 例 |
|---|---|---|
| 出来事を知らせる（過去形・購読者0人でも成立） | `Publish` / `Subscribe`（INotificationMessage） | CharacterDamaged / CharacterDied |
| してほしい（命令形・処理者は1基盤） | `PublishCommand` / `SubscribeCommand`（ICommandMessage）。二重処理者・処理者不在は**その場で例外** | ShowScreenCommand / PlayReactionCommand |
| 同期連鎖の外で処理したい | `PublishDeferred` → 合成ルートがフレーム末尾に `Pump()` | 連鎖の深い状態異常の波及 |
| 依頼して結果を受け取りたい | `RequestId` 相関: XxxRequested(RequestId,…)命令 → XxxCompleted(RequestId,成否)通知。発番は `RequestIdSource`（1プロセス1個） | セーブ依頼→完了通知 |
| 今の状態を同期的に知りたい | ServiceRegistry のIF | ICharacterQuery.TryGetPosition |
| 列挙・検索したい | ServiceRegistry のIF（バッファ詰め） | ICharacterRoster.Query(陣営, 生存) |
| 毎フレームの連続値 | メッセージに流さない。サービスIFで読む | カメラ追従の位置取得 |

- 購読の保持は `SubscriptionBag`（`.AddTo(bag)`）に統一。手書きの `List<IDisposable>` は書かない
- メッセージフローの観測は `MessageHub.MessagePublished` / `DeliveryFailed` フック
  （購読者の例外は後続の配達を止めず、ここへ集約される）

## ゲームフロー（Seed.Flow）——フェーズとステージの切り替え

永続ルート（MonoBehaviour 1枚。Hub/Services/Input/マスターデータ/カメラ/GameFlow を所有）
└ GameFlow（遷移状態機械。ChangePhaseCommand の唯一の処理者）
└ GamePhase（ホーム・戦闘・ショップ… 1フェーズ=1合成ルート）
OnEnter(payload) で基盤・舞台・画面を CompositionScope に組み立て
OnExit() で逆順に片付け、舞台GameObjectごと破棄する


- **遷移は必ず「要求→次Tickで Exit→（非同期ロード待ち）→Enter→PhaseChanged 通知」**。
  フェーズ自身の Tick 中に要求しても安全（遅延実行）。ロード中は LoadProgress でローディング表示
- **ステージ切り替え＝同じフェーズへ StageId を荷物にして再入**
  （`ChangePhaseCommand(Battle, stageId)`。GameFlow は同一フェーズでも Exit→Enter を完全に回す。
  ステージ選択もエリア移動も同じ経路）。ステージの中身はマスターデータ（StageSpec）が差し替える
- **シーンアセットを使うステージ**は `GamePhase.CreateLoadOperation` から
  `ISceneLoader.LoadScene`（実装: UnitySceneLoader）を返す——プリミティブ生成のデモは未使用の差し込み口
- **フェーズをまたいで生きる状態**（編成・所持金・進行度）はフェーズに置かず、
  永続ルートが持つサービス（ServiceRegistry 経由）に置く

## 1フレームの実行順（TickPipeline が型で保証する）

dt の供給源は時間基盤（GameClock）ただ1つ。永続ルートが毎フレーム最初に `clock.Tick(実dt)` を呼び、
ゲーム進行は ScaledDelta（ポーズ/ヒットストップで0、倍速/スローで伸縮）、
メニューやフェーズ遷移は UnscaledDelta を使う。操作は命令
（SetPausedCommand / SetTimeScaleCommand / HitStopCommand。処理者は GameClock のみ）。

コメント規約ではなく `App/Foundation/TickPipeline` に登録して回す。フェーズ昇順→登録順で決定的:

1. **Input** … 入力読取（Seed.Input の InputRouter）→ ManualLogic へ保管（＋ガード変化のみ Hub へ発行）
2. **Simulation** … CharactersManager.Tick。Logic→意図→Behavior遷移→姿勢→Avatar。
   攻撃遷移の成立時に BehaviorStarted → AttackRequested 発行 →（同期連鎖で）GameCore解決まで完了。
   **Tick 中に届いたリアクション命令は保留され、Tick 完了後に一括適用される**（順序の決定性を基盤が保証）
3. **LogicTime** … Bridge.AdvanceTime。**時間前進も「入力」としてジャーナルに乗る**
4. **Drain** … Bridge.Drain（翻訳表でレコード→通知）→（同期連鎖で）方針 → 行動割り込み・UI更新

## 決定的リプレイの規約（★最重要）

- ロジックを動かす入力は**すべて** `LogicInputFunnel.Submit`（記録してから実行）を通す。
  **Bridge はロジックの入力型を作るだけで、セクションを直接叩かない**
- 時間前進（AdvanceTime）も入力の一種としてジャーナルに記録する
- これにより統合経路（Hub経由のプレイ）がそのままリプレイ・検証・将来のロックステップ通信に使える
  （検証テスト: `HubIntegrationTests.Replay_JournalFromHubPlay_ReproducesSameResult`）
- 演出・通知はレコードに焼き込まれたスナップショット値だけで行う
  （レコードから実体（Sample_Unit等）を読み直すと「最終状態」しか見えず、多段ヒットの途中経過が壊れる）
- レコード→メッセージ翻訳は `RecordHubTranslator` の表に **Map / MapIgnore で全種を明示**。
  表に無い種は黙って捨てられず Unmapped で警告される。
  死亡などの重大事象はレコード（例: Defeated）としてロジックが明示発行し、消費側にHP推論をさせない

## ステージ自動生成（Seed.StageGen）——設計図と施工の分離

設定＋seed → GenerationPipeline（IGenerationPass の列。パスごとに random.Fork()）
→ StageBlueprint（設計図: セル種別/素材バリアント/バイオームの3レイヤー
＋区画＋配置物リスト。純C#・決定的）
→ StageBuilder（施工。アセットパレット＋配置表。抽選はしない）


- **同じ seed＋設定＋パス列 → バイト単位で同一の設計図**（見た目の抽選まで生成側で焼き込む）。
  ステージも決定的リプレイ・将来のロックステップの一部になる
- **拡張点はパス**（「数だけ用意する」流儀）: 迷路=穴掘り法 / 街=BSP区画＋道路＋建物 は
  標準パスの組み合わせにすぎない。洞窟・部屋型ダンジョンはパスを1つ書けば増える
- **アセット駆動**: 素材の複数登録は (バイオーム, セル, バリアント)→工場 のパレット
  （フォールバック連鎖つき＝未登録でもプリミティブで必ず建つ）。
  部屋・廊下の単位登録は RoomTemplate（文字列で手書き・配置物を内包可）。
  バイオームは素材・敵出現テーブル・パレットの分岐キー
- **配置物（敵・出口・ショップ・宝箱・イベント）**は Placement として設計図に載り、
  「踏んだら何が起きるか」は消費側（フェーズ）の配置表が決める（方針はApp）。
  未登録の配置種別は黙殺せず警告（翻訳表と同じ規約）

### アセットの紐付け（StagePaletteAsset ＋ Seed/Stage Palette）

```
StagePaletteAsset（ScriptableObject。素のSOにする＝Seed.Dataへ依存させない）
  ├ タイル行: (バイオーム, セル種別, バリアント) → プレハブ ＋ 高さ調整 ＋ セル拡縮
  └ 配置行: 配置種別 → プレハブ ＋ 高さ調整
        │ BuildPalette() / ApplyPlacements(builder)
        ▼
StageAssetPalette / StageBuilder（施工。抽選はしない）
```

- **未登録でも動く**: 埋めていない役割は内蔵プリミティブへ落ちるため、途中まで埋めた状態で試せる
- **キー重複はウィンドウが検出**: 同じキーの行は後の行に上書きされて黙って無効になるため、
  Play 前に検証で気づけるようにしている（実行時警告の先取り）
- 資産が持つのは「役割→プレハブ」だけ。抽選は生成側で設計図へ焼き込み済み（見た目まで決定的）

## モーション基盤（Seed.Motion）——アニメーション・姿勢・IK

Behavior遷移（真実） → RiggedAvatar（IAvatar実装）
├ AnimationDriver … Playables直駆動のクロスフェード再生（AnimatorControllerアセット不要）。
│   全身＋上半身の2レイヤー（AvatarMaskで範囲指定＝走りながら上半身だけ攻撃）。
│   正規化時間‰イベント（MotionSet登録）→ IAvatarEventSink → 行動側へ還流
└ MotionRig（LateUpdate） … アニメの上へ重ねる姿勢・IK
LookAtRig（注視・可動域クランプ）/ TwoBoneIkRig（腕・脚の解析解）/
ChainIkRig（FABRIK。尻尾・触手）/ SpringBoneRig（揺れもの: Verlet＋長さ拘束＋押し出し）/
FootIkRig（階段・段差・坂の接地適応。下記）

FootIkRig の構成（凹凸地形への追従）:
  IGroundProbe（地面問い合わせの契約。PhysicsGroundProbe が既定・差し替え可）
    → FootPlacementSolver（純C#の解決器。①必要上下量の測定 ②段差上限で足場判定
       ③深い側に合わせて腰を沈める ④法線へ足裏を沿わせる（傾斜上限つき）⑤時間追従で平滑化）
    → TwoBoneIkRig で脚を曲げ、足首を法線へ向ける


- **状態機械を二重に作らない**: 遷移の真実は Behavior。アニメ側は MotionSet
  （MotionClipId→クリップ＋再生仕様）の台帳と対応表（既定は同値素通し）だけ
- **当たり判定窓はデータ**: クリップを編集せず、正規化‰でイベント
  （HitboxBegin/End・コンボ窓・モーション終了）を登録できる。発火は純C#で決定的（テスト済み）
- **IKソルバーは自前の純数学**（外部パッケージ非依存・EditModeで数学的に検証済み）。
  適用は回転差分＝スキニングモデルでも破綻しない
- **足IKは段差・傾斜に上限を持つ**: 上限超過は「足場ではない」と判断してIKを切る
  （階段の蹴上げで脚が伸び切るのを防ぐ）。地面問い合わせを抽象化したので、
  階段・傾斜・穴を偽の地面として与えるEditModeテストが書ける（Physics非依存）。
  キャラ本体の高さは IMotionSolver の担当で、足元の凹凸だけを足IKが吸収する二段構成
- **揺れものは自前のVerletチェーン**（物理エンジン不使用＝調整しやすく決定的）。
  固定サブステップ＋dt上限でヒッチ・低FPSでも爆発せず、テレポート検出で鞭化を防ぐ。
  球コライダーで体へのめり込みを押し出す（純C#・EditModeテスト済み）
- **ルートモーションは RootMotionRelay で捕獲**し、位置へ直接適用しない
  （移動量を Tick 側が取り出して MotionSolver/Pose へ流す＝壁判定・リプレイと矛盾しない）
- リグは「艶」——真実（ActorPose・行動状態）には一切書き込まない

## カメラ基盤（Seed.Cameras）——視点の指名と合成

```
命令（どの基盤からでも）: SetViewpointCommand / PushViewpointCommand / PopViewpointCommand
        ▼
CameraDirector（命令の唯一の処理者。視点ID→CinemachineCameraの登録表）
  ├ ViewpointStack（純C#。基本の視点＝FPS/TPS常用切替 ＋ 重ねの視点＝演出カメラ）
  │   有効な視点 = 重ねがあれば最前面、無ければ基本 → 演出は積んで外すだけで元へ戻る
  └ 有効な視点のPriorityだけを上げる → CinemachineBrain が補間（＝合成）する
        ▼
通知: ViewpointChanged（HUDの切替・解析ログが購読）
```

- **見え方はCinemachineに委譲**（追従の減衰・遮蔽回避・ブレンド曲線）。基盤の価値は
  「他の基盤がカメラの実体を知らずに済む」ことに絞る
- 取り下げは順不同（演出が入れ違って終わっても破綻しない）。未登録IDへの切替は即例外
- 原点回帰に追従する: OriginShifted を購読して CinemachineCore.OnTargetObjectWarped を呼ぶ
  （呼ばないとカメラが「高速移動」と誤解して飛ぶ）
- 名前空間が複数形なのは UnityEngine.Camera 型との名前解決の衝突を避けるため

## プール基盤（Seed.Pooling）——使い回しと事故検出

```
ObjectPool<T>（純C#）… Rent/Return＋統計（生成/貸出/待機/ピーク/破棄）
  ├ 二重返却・他所からの返却は即 PoolException（症状が原因から離れる事故を発生点で止める）
  ├ 保持上限を超えた返却は捨てる（一時的な大量使用でメモリを抱え続けない）
  └ IPoolable（OnRent/OnReturn）＝リセットの置き場を型として持つ
GameObjectPool … 通知→非表示→親の付け戻し（位置・向きは貸出時に上書きするので触らない）
PoolRegistry  … プレハブごとのプールの台帳。PooledInstance（目印）から貸し主を辿る
```

- 標準の UnityEngine.Pool.ObjectPool ではなく自作したのは、二重返却の即例外・統計・
  IPoolable の3点を型で守るため（GameCore の EventPool と同じ方針の系列）
- プール量は統計の PeakRented を目安に決め、Discarded が 0 になるまで上限を上げる
- 依存ゼロの asmdef なので、どの基盤・App からでも使える

## 世界基盤（Seed.World）——原点回帰

```
OriginShifter（純C#。判断だけ）
  ├ 閾値: 焦点（プレイヤー）が Threshold を超えて原点から離れたか（既定は水平のみ）
  ├ 丸め: SnapSize の格子へ揃える（タイル・ノイズ模様の位相が飛ばない）
  └ 累積: TotalOffset を記録 → 見た目の座標 − TotalOffset ＝ 開始時からの絶対座標
        ▼
OriginShiftSystem（適用と告知。Tickで判断＝原点は世界の状態）
  ① 登録された根Transformをまとめて移動（地形・カメラ・プールの親。最上位だけ登録＝二重移動を防ぐ）
  ② IOriginShiftHandler へ通知（純C#側の座標＝ActorPose・トリガー・経路点は自分で加算）
  ③ Hub へ OriginShifted 通知（疎結合な追従。Cinemachineの履歴補正もここ）
```

- **なぜ必要か**: floatは絶対値が大きいほど刻みが粗くなり、静止しても表示が震え、
  当たり判定・IKが不安定になる。座標系を広げるのではなく世界を原点へ引き戻す
- **リプレイとの関係**: 原点移動は座標系の付け替えであってゲーム内容ではない。
  記録は絶対座標（ToAbsolute）で行うか、移動量も入力として記録する
- 足IKの時間追従は「前フレームとの差」で動くため、シフト時は ResetFollow() で捨てる

## 外部ライブラリの方針（依存の鉄則）

採用: UniTask / VContainer / LitMotion / MasterMemory(+MessagePack) / Addressables+Smart Addresser /
ZLogger / UnityDebugSheet / NuGetForUnity（詳細と使い方は `Docs/18_Libraries.md`）。

**外部ライブラリはすべて「殻」の道具**。次の層は依存禁止を維持する（asmdef が強制）:

| 層 | 外部依存 | 理由 |
|---|---|---|
| `Seed.Core`（GameCore） | 禁止 | 決定性の心臓部（async の再開フレームは非決定＝リプレイが壊れる） |
| `Seed.Hub.Contracts` / `Seed.Persistence` | 禁止 | エンジン非依存の契約・封筒（ヘッドレス検証の要） |
| その他基盤 | 必要最小限 | `Seed.Flow`→UniTask、`Seed.Assets`→UniTask+Addressables のみ |
| App 層 | 自由 | VContainer・LitMotion・DebugSheet はここだけ |

- DI（VContainer）が置き換えたのは合成ルートの new の配線だけ——通信は Hub、駆動順は
  TickPipeline / `Sample_GameLoop` がこれまで通り持つ
- マスターデータは「入力（SO/コード）→ ベイク（`Seed/Master Data Bake`）→
  実行時は MasterMemory バイナリ」。`MasterDataSet` の契約は不変（背面の差し替え）

## キャラクター基盤（Seed.Character）のアクター制御アーキテクチャ

CharactersManager（全体管理: 陣営の束・Tick順・名簿・リアクションの順序采配）
└ PlayersManager / EnemiesManager（陣営管理: FactionId付与・Add順にTick・退場時のAvatar解放まで一気通貫）
└ PlayerController / EnemyController（ユニット制御: LogicとAgentの結線）
├ Logic（頭脳: ManualLogic=手動 / AI思考ルーチンはApp側で実装）
└ CharacterAgent（1ユニット: 複数Actorの切替・リアクション受け口・生存写し・プール再利用）
└ CharacterActor（Presenter: Behavior状態機械＋ActorPose＋Avatar）
├ Behavior（行動の数だけ用意。CharacterBehaviorBase / TimedBehaviorBase を継承）
└ Avatar（View: Avatar3D=モデル / Avatar2D=立ち絵 / NullAvatar）


- **MonoBehaviour は Avatar の実装だけ**。Manager〜Behavior は純C#で、NUnit EditMode で直接テストできる
- **「状態は Tick、艶は Update」**——Avatar 側の Update に許されるのは色フェード等の純装飾のみ
- **位置・向きは純C#の ActorPose が真実の写し**。ICharacterQuery / ICharacterRoster も MonoBehaviour 非依存で答える
- **遷移の裁定は CharacterActor が一元化**（Behavior は「提案」のみ）。優先度
  （Locomotion=0 < Guard=10 < Attack=20 < Hit=30 < Death=100）と完了フラグの規則で、
  「攻撃中ガード不可」「被弾は攻撃を割り込む」「死亡は終端」が自然に成立する。
  同一行動への再入は AllowsRefresh の行動（多段ヒットのけぞり等）だけ許す。
  遷移通知の同期連鎖が自分へ再入したらキューで直列化する（表示と実状態のズレ防止）
- **Avatar→ロジックの唯一の逆流経路は IAvatarEventSink**。アニメの当たり判定フレーム・コンボ窓・
  モーション終了（AvatarEventId）を現在の行動へ届ける——秒数の手合わせではなくアニメ側の真実で駆動できる
- **基盤から Hub への発行はゼロ**。行動開始は C#イベント（Agent.BehaviorStarted）でAppへ渡し、
  AttackRequested への変換はAppの方針。Hubからは CharacterSystem が PlayReactionCommand を受け、
  CharactersManager の采配（Tick中は保留）を経て行動割り込みへ翻訳する

### AI基盤（Seed.AI）——キャラクターAIとメタAI

AiDirector（メタAI: 戦場全体の采配。指示書 AiOrders を配る＝攻撃権・手心・注目対象）
└ AiBrain（キャラAI: ICharacterLogic 実装。Consideration を採点し最高得点の意図を採用）
└ IAiConsideration（思考の1候補。「追う」「攻撃する」…候補の数だけ用意する拡張点）
出力は CharacterIntent（＝入力と同じ語彙）


- **制御の経路はプレイヤーもNPCもエネミーも完全に共通**。AIは意図（CharacterIntent）を
  出すだけで、Controller/Agent/Behavior から見れば手動入力と区別がつかない
- **入力のエミュレート**: InputEmulator が AIの意図を ManualLogic へ「入力として」注入する。
  自動操縦・デモプレイ・カットシーン誘導が、制御系を1行も変えずに実現できる（統合デモの [O]）
- **メタAIは指示書（AiOrders）でしか介入しない**——ユニットを直接動かさないので、
  外せば各ユニットは自律で動き続ける。攻撃の同時数制限は AttackTokenPool
- 採点は登録順走査・同点先勝ちで決定的。記憶は AiBlackboard（AI時計つき）
- 具体的な採点ルール（Consideration）と采配ルール（Director派生）は方針＝アプリ側に置く

### 内容を増やすときの入口（増やしやすさの規約）

| 増やしたいもの | やること |
|---|---|
| キャラ・ユニット | 定義（Seed.Data の IEntityDefinition）を1件足す → CharacterDefinition へ写して CharacterFactory.Create |
| 行動（回避・詠唱・必殺技…） | CharacterBehaviorBase / TimedBehaviorBase を継承した小クラスを1つ書き、CharacterDefinition.WithBehavior で装着。BehaviorKey は100以降 |
| リアクション | ReactionId を100以降で発番。標準表に無いIDは同値の BehaviorKey への遷移要求として自動で素通しされる（基盤変更ゼロ） |
| 陣営 | UnitManager を継承して CharactersManager.AddManager（FactionId は3以降） |
| 画面 | UIScreen を継承（Layer で Base/Overlay/Modal を宣言）→ UISystem.Register |
| マスターデータ | EntityDefinitionData 継承のDTO（JSON/SO/コード）→ MasterDataSet → MasterDataLoader.RegisterAll |
| メッセージ | タイトル固有なら `Game.<Title>.Contracts` へ。`INotificationMessage` / `ICommandMessage` を必ず付ける |
| フェーズ（ホーム/戦闘/ショップ…） | GamePhase を継承して GameFlow.AddPhase 1行。PhaseId はアプリ定数 |
| AIの思考（追う/逃げる/詠唱…） | IAiConsideration を1クラス書いて AiBrain.With で装着（採点の重みがゲームの個性） |
| メタAIの采配 | AiDirector を継承して指示書（AiOrders）の発行ルールを書く。同時攻撃数は AttackTokenPool |
| 画面の出入り演出 | UIScreen.CreateShow/HideTransition で IScreenTransition を返す（既定null=即時。Router.Tick が駆動） |
| 移動の物理/経路制約 | IMotionSolver 実装を CharacterActor.MotionSolver へ差す（既定は素通し。CharacterController実装あり） |
| セーブ・リプレイ保存 | byte[] にして ISaveStore（ServiceRegistry経由）へ。封筒検証・原子的書き込みは基盤持ち |
| ステージ | StageSpec（マスターデータ）を1件足す → ChangePhaseCommand(Battle, stageId) で出撃 |
| モーション | MotionSet.Add 1行（クリップ＋‰イベント）。BehaviorKeyと番号を揃えれば対応表も不要 |
| 姿勢・IK | LookAtRig / TwoBoneIkRig / ChainIkRig / FootIkRig を MotionRig.With で装着 |
| 視点（FPS/TPS/演出） | CinemachineCamera を作り `CameraDirector.Register(ViewpointId, camera)` 1行。切替は命令1発 |
| 使い回す実体（弾・エフェクト） | `PoolRegistry.Rent(prefab, …)` と `Return`。リセットは IPoolable |
| 広大なフィールド | `OriginShiftSystem` に根 Transform と追従契約を登録（純C#の座標は自分で加算） |
| 生成ステージの美術 | メニュー `Seed/Stage Palette` でプレハブを割り当て、`BuildPalette()` を渡す |
| 揺れもの（髪・尻尾・マント） | ボーン列を SpringBoneRig へ渡し MotionRig.With で装着（球コライダー任意） |
| UI部品（ボタン等） | SeedButton / SeedToggle / SeedSlider（uGUI継承。連打防止・IDisposable購読・SEフック内蔵） |
| 非同期ロード | UniTask で書いて UniTaskFlowOperation / IAssetLoader に載せる（殻限定） |
| ログ | GameLog.CreateLogger(基盤名) → ZLog 系で書く |
| 地形の種類（洞窟・塔…） | IGenerationPass を1つ書いてパイプラインに Add |
| 部屋・廊下の手作りユニット | RoomTemplate.Parse の文字列を1つ書いて TemplateRoomsPass へ |
| ステージ素材（床・壁のアセット） | StageAssetPalette.Bind(バイオーム, セル, バリアント, プレハブ工場) を1行 |
| バイオーム | BiomeId 発番＋バリアント重み表・出現テーブル・パレットに行を足す |
| フィールド配置物（出口・宝箱・イベント…） | PlacementKind 発番 → LandmarkPlacementPass 1行＋施工の SetPlacement 1行 |

## 動く実例

`Seed.App/Samples/Sample_GameFlowRunner`（空のGameObjectにアタッチでPlay可能）。
ホーム: [1]草原 / [2]火山 / [3]ショップ / [4]迷宮（自動生成） / [5]市街（自動生成）。
戦闘: WASD移動 / [1]攻撃 / [G]ガード / [T]3D⇔2D切替 / [O]自動操縦 / [P]ポーズ / [B]ホームへ。
純C#部分の統合テストは `App/Tests/Editor/HubIntegrationTests.cs`（リプレイ再現テスト含む）と
`Flow/Tests/Editor/GameFlowTests.cs`（フェーズ遷移・ステージ再入・非同期ロード）。