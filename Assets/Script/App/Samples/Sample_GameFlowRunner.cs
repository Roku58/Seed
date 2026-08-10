// ============================================================================
// 【サンプルコード】Sample_GameFlowRunner
// ゲーム全体のフロー（ホーム⇄戦闘⇄ショップ）を回す永続ルート（VContainer 版）。
//
// 空のGameObjectにアタッチしてPlayするだけで動く。
//   ホーム : [1] 草原へ出撃 / [2] 火山へ出撃 / [3] ショップ / [4] 迷宮 / [5] 市街 / [6] 揺れものデモ / [7] イベントADV(3D) / [8] イベントADV(2D)
//   戦闘   : WASD移動 / [1]攻撃 / [G]ガード / [T]3D⇔2D切替 / [C]視点切替 / [B]ホームへ
//
// [VContainer の役割分担]
//   Configure（このクラス）… 「何を作り、誰に何を渡すか」の宣言（生成と寿命）
//   Sample_GameLoop        … 「どの順で初期化し、毎フレーム何を回すか」（駆動順）
//   各フェーズ（GamePhase）… その場でだけ使う基盤・舞台・画面（1フェーズ=1合成ルート）
//
// DI が置き換えたのは手書きの new の配線だけで、Hub（通信）・TickPipeline（駆動順）・
// フェーズ内の CompositionScope（逆順片付け）はそのまま——コンテナは万能の置き場ではなく、
// 「オブジェクトの構築と寿命」だけを受け持つ。
// ============================================================================

using Seed.Clock;
using Seed.Flow;
using Seed.Hub;
using Seed.Hub.Contracts;
using Seed.Input;
using VContainer;
using VContainer.Unity;

namespace Seed.App
{
    /// <summary>【サンプル】永続ルート（VContainer の LifetimeScope。ゲームの入口）。</summary>
    public sealed class Sample_GameFlowRunner : LifetimeScope
    {
        /// <summary>依存の宣言（生成順はコンテナが解決し、初期化順は GameLoop が持つ）。</summary>
        protected override void Configure(IContainerBuilder builder)
        {
            // 仲介基盤（IDisposable は登録の逆順で自動 Dispose される）
            builder.Register<MessageHub>(Lifetime.Singleton);
            builder.Register<ServiceRegistry>(Lifetime.Singleton);

            // 時間基盤 → フロー基盤の順に登録（Dispose は逆順＝フローが先に畳まれる）
            builder.Register<GameClock>(Lifetime.Singleton);
            builder.Register<GameFlow>(Lifetime.Singleton);

            // 入力基盤（Reader の差し替えはこの1行。実プロジェクトは InputSystemReader へ）
            builder.Register<Sample_KeyboardReader>(Lifetime.Singleton).As<IInputReader>();
            builder.Register<InputRouter>(Lifetime.Singleton);

            // マスターデータ（ベイク済みバイナリがあれば MasterMemory から、無ければコード直書き）
            builder.RegisterInstance(Sample_MasterBinary.LoadCatalog(
                new CharacterId(1), new CharacterId(2)));

            // フェーズ（追加＝ここに1行＋GameLoop の AddPhase に1行）
            builder.Register<Sample_HomePhase>(Lifetime.Singleton);
            builder.Register<Sample_BattlePhase>(Lifetime.Singleton);
            builder.Register<Sample_ShopPhase>(Lifetime.Singleton);
            builder.Register<Sample_AdvEventPhase>(Lifetime.Singleton);

            // 駆動役（IStartable/ITickable/ILateTickable が PlayerLoop に載る）
            builder.RegisterEntryPoint<Sample_GameLoop>();
        }
    }
}
