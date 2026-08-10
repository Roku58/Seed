# 19. イベントADV（Seed.Adv）——文字送り・吹き出し・選択肢・演技

[← 前: 18_Libraries](18_Libraries.md) | [索引](00_Roadmap.md)

## この章で分かること

- ローグライクのイベントADV（ショップ・会話イベント）を統合デモで動かす方法
- 文字送り・ページ送り・選択肢の状態機械（`AdvPlayer`）の使い方
- 会話の見せ方2種（固定テキストボックス / 吹き出し）と、吹き出しを0〜複数枚重ねる方法
- 会話の進行に合わせた「演技（`AdvAct`）」——移動・モーション・モデル読み替え・
  カメラの寄り引き・タイムライン起動
- 3D舞台（モデル）と2D舞台（立ち絵）の使い分け（`StageMode`）
- UIのガワをプレハブで丸ごと差し替える方法と、再生中の入力ロックのデータ指定
- 演出の寿命（時間・ページ切替・送り操作）と強制的に閉じる命令
- マスターデータからタイムライン（ブック）を自動ビルドして再生の基本経路にする方法
- Cinemachine の複数カメラをデータで切り替える方法と、台本からコードへの合図（Signal）

## 前提

- [01_Demo](01_Demo.md) …… 統合デモの起動方法
- [07_Data](07_Data.md) …… マスターデータの流れ（定義→ベイク→実行時）
- [06_UI](06_UI.md) …… SeedButton（タップとコントローラ決定の両対応ボタン）

## 1. これは何か

イベントADVの**進行の真実**だけを持つ小さな基盤です。テキストを文字送りで表示し、
送り操作（タップ・決定）で「全文表示 → 次ページ → 選択肢 or 終了」と進む状態機械
（`AdvPlayer`）と、その台本（`AdvScript`）でできています。

> 📖 **用語 — 文字送り**: テキストを1文字ずつ表示していく演出。表示中に送り操作をすると
> 残りが一括で表示され、全文表示後の送り操作で次のページへ進むのが定番の作法です。

表示（uGUI）・入力（タップ / EventSystem）・演技の実行（移動・アニメ再生）は
すべて表示側の仕事で、基盤は純C#・依存ゼロ——だから EditMode テストで
「送り→ページ→選択→分岐」の仕様を固定できます。

## 2. 動かして試す

**3Dサンプル（[7] よろず屋）**:

1. 統合デモを Play し、ホームで **[7]** を押す
2. 相棒（UnityChan）が歩いて入店し（**演技**）、店主の吹き出しが出る。2ページ目では
   相棒の吹き出しと店主の独り言の吹き出しが**同時に2枚**出る（吹き出しは0〜複数）
3. **画面のどこかをタップ**（またはコントローラ決定）——文字送り中なら一括全文表示、
   全文表示後なら次ページへ。3ページ目は**固定テキストボックス**表示
4. 選択肢はタップで確定、またはコントローラ/矢印キーの上下で選んで決定
   （Unity 標準の EventSystem ナビゲーション）
5. 「大剣を研いでもらう」→ **Cinemachine カメラが店主の寄りへ切り替わり**（CameraSwitch）、
   ページが進むと**寿命（ページ切替）で自動的に既定カメラへ戻る**。研ぎ上がりでは
   台本からコードへ合図が飛ぶ（Signal → Console に「シグナル受信: SharpenDone」）
6. 店では [B] でいつでも退出できるが、購入・研ぎの強制イベント中は
   **入力ロック（データ指定）**で [B] が効かない

**2Dサンプル（[8] 幕間の会話）**:

1. ホームで **[8]** を押す——舞台は3Dモデルではなく**キャンバス上の立ち絵**
2. 立ち絵が左右から**スライドイン**（同じ Move の演技が2Dでは立ち絵の座標を動かす）
3. 吹き出しは立ち絵の位置＋オフセット（キャンバスpx）に追従する
4. 立ち絵はプレハブ未指定なので内蔵の色板＋名前で代替される——
   `actorPrefabs` に uGUI プレハブ（RectTransform 付き）を指定すれば差し替わる

## 3. コードで使う

最小の使い方（台本を組んで進行させる）:

```csharp
var script = new AdvScript(
    new[]
    {
        new AdvPage("いらっしゃい。", speakerId: 1, AdvSpeechStyle.Bubble),
        new AdvPage("ゆっくり見ていって。"), // 既定は窓表示・ナレーション
    },
    new[] { new AdvChoice("買う", 302), new AdvChoice("出る", 0) });

var player = new AdvPlayer(script, charsPerSecond: 30f);
player.Finished += resultId => { /* 分岐はアプリの方針（次のイベントへ 等） */ };

// 毎Tick: 文字送りを進め、表示は VisibleLength を写すだけ
player.Tick(deltaTime);
text.text = player.PageText.Substring(0, player.VisibleLength);

// 送り操作（タップ・決定ボタン）と選択の確定
player.Advance();
player.Select(index);
```

演技はページに載せます（実行は表示側——デモの実装は `Sample_AdvEventPhase.ExecuteActs`）:

```csharp
new AdvPage("すごい、本当にお店があった……！", speakerId: 2, AdvSpeechStyle.Bubble,
    new[]
    {
        new AdvAct(2, AdvActKind.Move, x: -0.85f, z: 2.3f, seconds: 1.5f), // 入店の歩き
        new AdvAct(2, AdvActKind.Motion, "Win"),                            // 喜びのモーション
    });
```

## 4. 仕組み

進行は4状態の状態機械です:

1. **Typing（文字送り中）** … `Tick(dt)` が「見せてよい文字数」を進める。
   `Advance()` で残りを一括全文表示
2. **PageComplete（全文表示済み）** … `Advance()` で次ページへ。最終ページなら
   選択肢があれば **Choosing** へ、無ければそのまま **Finished**（強制イベント）
3. **Choosing（選択肢の提示中）** … `Select(index)` で確定。選択カーソルの真実は
   基盤ではなく **UI（EventSystem）** が持つ——真実を二重に持たない
4. **Finished** … `ResultId`（選んだ選択肢の結果 / 強制イベントは 0）を通知

デモの配線（`Sample_AdvEventPhase`）の分担:

- **送り** … 画面全体に敷いた透明な `SeedButton`（タップ）。キーボード・コントローラの
  決定は EventSystem が選択中のボタンへ Submit として届ける（Unity 標準機能に委譲）
- **吹き出し** … 雛形の複製で**0〜複数枚**を同時に出せる。話者の吹き出し（文字送り）に加え、
  演技 `Bubble` で静的な吹き出しを重ね、`BubbleClear` や表示時間で消す。
  位置は 3D=ワールド座標のスクリーン投影 / 2D=立ち絵の位置＋オフセット
- **演技** … ページ開始時に実行される舞台指示。移動=LitMotion / モーション=
  `CrossFadeInFixedTime` / モデル読み替え / 登場退場 / カメラ（手動移動 or
  **CameraSwitch**=事前定義した Cinemachine カメラの切替。ブレンドは Brain）/
  タイムライン起動（PlayableDirector 入りプレハブ）/ **Signal**（コードへの合図）
- **演技の実行経路** … **Seed/Adv Timeline Build** でマスターデータから
  「1ページ=1タイムライン・束=ブック」を自動生成すると、以後はタイムライン再生が
  基本経路になる（演技はマーカー `Sample_AdvActMarker` として載り、生成後に
  エディタで時刻をずらしたりトラックを足せる。再ビルドは全面上書き）。
  ブックが無いイベントはマスターデータ直接実行のフォールバックで動く
- **寿命と閉じ命令** … Bubble/Timeline/Camera 系の演出は `actLives`
  （0=閉じ命令まで / 1=ページ切替で / 2=送り操作で）と時間（Seconds）で自動で閉じ、
  強制的に閉じる命令も対で持つ（`BubbleClear` / `TimelineStop` / `CameraReset`）
- **Signal** … 演技 `Signal` が Hub 通知 `Sample_AdvSignal`（イベントID・合図名）を発行し、
  購読側が任意の処理を行う（実例: `Sample_GameLoop` がログ出力を購読。
  報酬付与・フラグ更新はここに書く）
- **舞台モード** … `StageMode` 0=3D（ワールドにモデル・カメラ演技あり）/
  1=2D（キャンバスに立ち絵。座標・オフセットはキャンバスpx。カメラ演技は無効）
- **UIのガワ** … `UiPrefabKey` に `Sample_AdvUiView` 付きプレハブを指定すれば丸ごと
  差し替え・上書きできる（コード生成の内蔵ガワはフォールバック）
- **入力ロック** … `LockOtherInput`（既定=停止）が、再生中に ADV 操作以外の入力
  （[B] 退出など）を受けるかをイベントごとに決める
- **連鎖** … `Finished` の結果ID（または強制イベントの `NextEventId`）で
  次のイベントを同フェーズ内で読み直す。0 ならホームへ

## 5. 増やす・拡張する

| 増やしたいもの | やること |
|---|---|
| イベント（会話・ショップ） | `Sample_MasterCatalog.AddEvents` に `Sample_EventSpec` を1件足す → `Seed/Master Data Bake` |
| ページ・話者・見せ方 | `pageTexts` / `pageSpeakers` / `pageStyles`（0=窓 / 1=吹き出し）に1要素ずつ足す |
| 演技 | `actPages`（対象ページ）＋`actActors`/`actKinds`/`actKeys`… に1要素ずつ足す |
| 登場キャラ | `actorIds`〜`bubbleZ` に1要素ずつ足す（プレハブは Resources パスで指定） |
| 装飾のバリエーション | 背景= `backgroundPrefabKey`、キャラ= `actorPrefabs` / `actorControllers` を差し替える |
| UIのガワ | `Sample_AdvUiView` をルートに付けた Canvas プレハブを作り `uiPrefabKey` で指定（雛形ボタン・雛形吹き出しを含める） |
| カメラの寄り引き | 演技 Kind=8（Camera）: X/Y/Z=カメラ位置・ActorId=注視キャラ・Seconds=移動時間（3D舞台のみ） |
| 2Dの会話 | `stageMode: 1` にして座標・吹き出しオフセットをキャンバスpxで書く（立ち絵は uGUI プレハブ） |
| 入力ロックの有無 | `lockOtherInput`（既定 true=停止。店のように [B] で出たい場合だけ false） |
| タイムライン演出 | PlayableDirector 入りプレハブを作り、演技 Kind=7（Timeline）の Key で起動（Kind=9 TimelineStop で強制終了） |
| 演出の寿命 | `actLives` に 1（ページ切替で閉じる）/ 2（送り操作で閉じる）を指定。時間は Seconds |
| ブックのビルド | メニュー **Seed/Adv Timeline Build**（削除メニューで直接実行へ戻せる） |
| カメラ切替 | `camIds`〜`camLookActors` でカメラを定義し、演技 Kind=12（CameraSwitch。ActorId=カメラID）で切替。Kind=10（CameraReset）で既定へ |
| コードへの合図 | 演技 Kind=11（Signal。Key=合図名）→ `Sample_AdvSignal` を購読して任意の処理 |
| 演技の種類そのもの | `AdvActKind` に1種足し、表示側の `ExecuteAct` に対応を1つ書く |

## 6. よくあるつまずき

- **症状**: 選択肢がコントローラで選べない → **原因**: シーンに EventSystem が無い →
  **対処**: デモは `Sample_AdvEventPhase` が自前で作る。自作フェーズでも
  `EventSystem`＋`InputSystemUIInputModule` を必ず用意する
- **症状**: 吹き出しが出ない → **原因**: ページの話者ID（`pageSpeakers`）が
  登場キャラ表（`actorIds`）に無い → **対処**: ID を合わせる（不一致は警告ログが出る）
- **症状**: 演技のモーションが再生されない → **原因**: `actKeys` のステート名が
  そのキャラの AnimatorController に無い → **対処**: Controller のステート名と揃える
  （StarterAnimator: Idle/Locomotion/Jump/InAir/Attack/Guard/Hit/Death/Win）

## 7. 関連ファイルとテスト

- `Assets/Script/Adv/Runtime/AdvScript.cs` — 台本（ページ・話者・見せ方・演技・選択肢）
- `Assets/Script/Adv/Runtime/AdvPlayer.cs` — 進行の状態機械
- `Assets/Script/Adv/Tests/Editor/AdvTests.cs` — 進行仕様のテスト（12件）
- `Assets/Script/App/Samples/Sample_EventSpec.cs` — イベントのマスターデータ定義
- `Assets/Script/App/Samples/Sample_AdvEventPhase.cs` — デモの配線（画面・吹き出し・演技・カメラ・2D/3D舞台）
- `Assets/Script/App/Samples/Sample_AdvUiView.cs` — UIガワの結び付け（プレハブ差し替えの契約）

[← 前: 18_Libraries](18_Libraries.md) | [索引](00_Roadmap.md)
