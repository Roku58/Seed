# GameCore スタートアップガイド

新しくこの基盤を触る人向けの「最短で動かして、最小のゲームを載せる」ための手引き。
仕様の詳細やパターン集は `IMPLEMENTATION_GUIDE.md`、経緯・設計原則は `DESIGN_NOTES.md` を参照。

---

## 1. これは何か（30秒）

「ゲーム内で何かが起きたとき、結果を計算して記録する計算係」。

- 入力（誰が何をした）を渡すと、ルールを全部適用して、結果を**事象レコードの列**で返す
- 画面表示・アニメ・移動は担当外（View層・アクション層の仕事）
- 同じシード＋同じ入力なら**必ず同じ結果**（リプレイ・テスト・検証ができる）

## 2. 導入

1. `Assets/Script/GameCore/` フォルダごとプロジェクトにコピー（本プロジェクトには導入済み）
2. 自分のゲーム用 asmdef から `Seed.Core` を参照に追加
   （asmdef を使っていない場合は何もしなくてよい。Assembly-CSharp から自動で参照される）
3. `using Seed.Core;` を書けば準備完了

構成:

```
Runtime/       … コア（ロジックはこれだけ参照。UnityEngine非依存）
Presenter/  … 演出ブリッジ（レコード→UI/演出の定型部品。UnityEngine依存OK）
Samples/       … 実装例2種（Sample_ プレフィックス。読むための教材。削除してもRuntimeは無傷）
Tests/         … コア・ブリッジ・サンプルのEditModeテスト
Docs/          … 本ガイドと実装ガイド
```

## 3. 5分で動かす

1. 空のシーンに空の GameObject を作る
2. `Sample_CommandBattleRunner`（ターン制）または `Sample_ActionBattleRunner`（アクション）をアタッチ
3. Play → Console に事象レコードが流れる
4. Inspector の `Enable Kernel Trace` を ON にすると、コア内部の動きも見える
5. テスト: Window > General > Test Runner > EditMode > Run All（全部緑になることを確認）

アクション側のデモは最後に「リプレイ検証 OK」と出る。これは記録した入力列＋シードから
試合を再計算し、結果のハッシュが一致したことを意味する（＝決定性が保たれている証拠）。

## 4. 最小のゲームを載せるチュートリアル（10分）

「宝箱を開けたら報酬がもらえる。『幸運のお守り』を持っていると報酬2倍」という
ミニ事象システムを最初から書く。バトルでなくてもこの基盤に載る、という例でもある。

```csharp
using System.Collections.Generic;
using Seed.Core;

namespace MyGame.Treasure
{
    // 1) レコード＝「このゲームで起こりうること」を最初に決める（設計の起点）
    public enum RecordKind { ChestOpened, RewardGranted }

    public readonly struct Record
    {
        public readonly RecordKind Kind;
        public readonly int Gold;
        public Record(RecordKind kind, int gold) { Kind = kind; Gold = gold; }
    }

    // 2) イベント＝個別仕様が口を出せるポイント（sealed + Reset が規約）
    public sealed class RewardAmountEvent : LogicEvent
    {
        public int Gold;
        public override void Reset() { Gold = 0; }
    }

    // 3) セクション＝処理の流れだけを書く。「お守りで2倍」はここに書かない！
    public readonly struct OpenChestInput
    {
        public readonly int BaseGold;
        public OpenChestInput(int baseGold) { BaseGold = baseGold; }
    }

    public sealed class OpenChestSection : Section<OpenChestInput, int>
    {
        public static readonly OpenChestSection Instance = new OpenChestSection();
        public override string Name => "宝箱を開ける";
        private OpenChestSection() { }

        public override int Execute(LogicContext ctx, in OpenChestInput input)
        {
            var log = ctx.GetExtension<RecordLog<Record>>();
            log.Add(new Record(RecordKind.ChestOpened, 0));

            int gold;
            using (var scope = EventScope<RewardAmountEvent>.Rent()) // 借りたら自動返却
            {
                scope.Event.Gold = input.BaseGold;
                ctx.Hub.Fire(scope.Event, ctx);   // ← お守り等がここで介入
                gold = scope.Event.Gold;
            }

            log.Add(new Record(RecordKind.RewardGranted, gold));
            return gold;
        }
    }

    // 4) ハンドラー＝個別仕様。1仕様1クラス。登録すれば存在し、しなければ存在しない
    public sealed class LuckyCharmHandler : LogicEventHandlerBase,
        ILogicEventHandler<RewardAmountEvent>
    {
        public LuckyCharmHandler() : base(owner: null) { }
        public override void RegisterTo(EventHub hub) => hub.Subscribe<RewardAmountEvent>(this);

        public void Handle(RewardAmountEvent ev, LogicContext ctx)
        {
            ev.Gold = Permille.Apply(ev.Gold, 2000); // 2倍（整数‰演算）
        }
    }

    // 5) 合成ルート＝実行
    public static class TreasureDemo
    {
        public static void Run()
        {
            var ctx = new LogicContext(logicSeed: 1);
            ctx.AddExtension(new RecordLog<Record>());
            new LuckyCharmHandler().RegisterTo(ctx.Hub); // ←この行を消せば「お守り無し」の世界

            ctx.BeginResolution();
            var gold = ctx.RunSection(OpenChestSection.Instance, new OpenChestInput(100));
            // gold == 200。レコードには ChestOpened → RewardGranted(200) が並ぶ
        }
    }
}
```

書く順番はいつもこう: **レコード → イベント → セクション → ハンドラー → 合成ルート**。
これで足りなくなったら `IMPLEMENTATION_GUIDE.md` のパターン集へ進む。

## 5. 次に読むもの

- `IMPLEMENTATION_GUIDE.md` … 全機能のパターン集（乱数規約・巻き戻し・リプレイ・優先度…）
- `Samples/CommandBattle/Sample_CommandBattleDemo.cs` … 合成ルートの実例（参照方式レコード＋巻き戻し）
- `Samples/ActionBattle/Sample_ActionBattleDemo.cs` … ID方式レコード＋リプレイ検証の実例
- `DESIGN_NOTES.md` … 経緯・設計原則・CEDEC講演の要点・TODO

## 6. FAQ

- **コンパイルエラーが出た** → まず `Seed.Core` asmdef への参照を確認。次に Unity バージョン（2021.3以降）
- **Test Runner にテストが出ない** → EditMode タブを見ているか確認
- **サンプルを消したい** → `Samples/` フォルダごと削除してよい（Tests の一部も一緒に消すこと）
- **float を使いたい** → 使わない。倍率は ‰（Permille）、時間は ms（long）で表す。理由はガイド§7
