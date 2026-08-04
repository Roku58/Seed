using System;
using System.Collections.Generic;

namespace Seed.Core
{
    /// <summary>
    /// 「キー → 生成方法」の汎用レジストリ。
    ///
    /// 「技→まもるハンドラーの紐付け」（コマンドバトルサンプル参照）のような対応表を一般化したもの。
    /// 「同じデータでも、タイトルや構成によって生成される振る舞いを差し替える」
    /// ための合成ルート用部品。
    ///
    /// 例:
    /// <code>
    ///   var skills = new FactoryRegistry&lt;SkillId, Hunter, LogicEventHandlerBase&gt;();
    ///   skills.Bind(SkillId.AttackBoost, h => new AttackBoostHandler(h, 1100));
    ///   if (skills.TryCreate(SkillId.AttackBoost, hunter, out var handler))
    ///       handler.RegisterTo(ctx.Hub);
    /// </code>
    ///
    /// ScriptableObject でデータ駆動化する場合も、最終的にこのレジストリへ
    /// 変換して流し込む形にするとコアはデータ形式を知らずに済む。
    /// </summary>
    public sealed class FactoryRegistry<TKey, TArg, TProduct>
    {
        private readonly Dictionary<TKey, Func<TArg, TProduct>> _map =
            new Dictionary<TKey, Func<TArg, TProduct>>();

        /// <summary>件数。</summary>
        public int Count => _map.Count;

        /// <summary>
        /// 生成方法を登録する。既定では同キーの二重バインドを構成ミスとして例外にする
        /// （黙った上書きはタイトル構成のバグを隠すため）。意図的な差し替えは allowOverwrite: true。
        /// </summary>
        public void Bind(TKey key, Func<TArg, TProduct> factory, bool allowOverwrite = false)
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }
            if (!allowOverwrite && _map.ContainsKey(key))
            {
                throw new LogicException($"キー {key} は既にバインド済み（差し替えなら allowOverwrite: true）");
            }
            _map[key] = factory;
        }

        /// <summary>指定キーが登録済みかを返す。</summary>
        public bool Contains(TKey key)
        {
            /// <summary>ContainsKey を生成する。</summary>
            return _map.ContainsKey(key);
        }

        /// <summary>キーに対応する生成方法があれば生成する。未登録＝そのタイトルに存在しない仕様。</summary>
        public bool TryCreate(TKey key, TArg arg, out TProduct product)
        {
            if (_map.TryGetValue(key, out var factory))
            {
                product = factory(arg);
                return true;
            }
            product = default;
            return false;
        }
    }
}
