using UnityEngine;

namespace Seed.Character
{
    /// <summary>
    /// レンダラーの色替えを MaterialPropertyBlock で行う共通ユーティリティ。
    ///
    /// renderer.material への代入はマテリアルを暗黙に複製（インスタンス化）し、
    /// キャラが増えるほどメモリとバッチングを蝕む——色のような per-instance の
    /// 値は MPB で上書きするのが正道。URP（_BaseColor）と Built-in（_Color）の
    /// 両プロパティへ書くため、パイプラインを問わず効く。
    /// </summary>
    public static class RendererTint
    {
        /// <summary>URP系のカラープロパティID。</summary>
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        /// <summary>Built-in系のカラープロパティID。</summary>
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        /// <summary>使い回しのブロック（メインスレッド専用）。</summary>
        private static MaterialPropertyBlock _block;

        /// <summary>色を上書きする（マテリアルは複製されない）。</summary>
        public static void Set(Renderer renderer, Color color)
        {
            if (renderer == null)
            {
                return;
            }
            var block = _block ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, color);
            block.SetColor(ColorId, color);
            renderer.SetPropertyBlock(block);
        }

        /// <summary>
        /// 現在の見た目の色を読む（MPBの上書きがあればそれ、無ければ共有マテリアルの色。
        /// どちらの読み取りもマテリアルを複製しない）。
        /// </summary>
        public static Color Get(Renderer renderer)
        {
            if (renderer == null)
            {
                return Color.white;
            }
            var block = _block ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            if (!block.isEmpty)
            {
                return block.GetColor(BaseColorId);
            }
            var shared = renderer.sharedMaterial;
            if (shared != null && shared.HasProperty(BaseColorId))
            {
                return shared.GetColor(BaseColorId);
            }
            if (shared != null && shared.HasProperty(ColorId))
            {
                return shared.GetColor(ColorId);
            }
            return Color.white;
        }
    }
}
