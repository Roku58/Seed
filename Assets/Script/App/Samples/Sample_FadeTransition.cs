using System;
using LitMotion;

namespace Seed.App
{
    /// <summary>
    /// 【サンプル】LitMotion による画面のフェード演出（IScreenTransition 実装）。
    ///
    /// トゥイーンの値は Action で画面側の任意の値（アルファ・スケール・位置）へ流す——
    /// インスペクター設定ではなくコードから制御する前提の作りで、
    /// どの画面にも同じ1クラスで差し込める。
    ///
    /// LitMotion は自前の PlayerLoop で進むため Tick では何もしない
    /// （IScreenTransition の Tick 契約はポーリング型演出のためにあり、両立してよい）。
    /// ゼロアロケーション・struct ベースなので、画面遷移のたびに GC を積まない。
    /// </summary>
    public sealed class Sample_FadeTransition : Seed.UI.IScreenTransition
    {
        /// <summary>走っているモーション。</summary>
        private MotionHandle _handle;

        /// <summary>フェードを開始する（生成した瞬間から走る）。</summary>
        public Sample_FadeTransition(float from, float to, float seconds, Action<float> apply)
        {
            _handle = LMotion.Create(from, to, seconds)
                .WithEase(Ease.OutQuad)
                .Bind(apply);
        }

        /// <summary>完了したか。</summary>
        public bool IsDone => !_handle.IsActive();

        /// <summary>LitMotion 側が進めるため何もしない。</summary>
        public void Tick(float deltaTime)
        {
        }

        /// <summary>終端へ飛ばす（スキップ・割り込み時に Router が呼ぶ）。</summary>
        public void Complete()
        {
            if (_handle.IsActive())
            {
                _handle.Complete();
            }
        }
    }
}
