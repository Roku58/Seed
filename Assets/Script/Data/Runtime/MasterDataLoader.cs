using System;
using System.Collections.Generic;
using Seed.Core;

namespace Seed.Data
{
    /// <summary>
    /// MasterDataSet を Seed.Core の各レジストリへ流し込む変換役（純C#）。
    ///
    /// [位置づけ] Seed.Data における「出口」。入口（SO / JSON / コード）が何本増えても、
    /// Core に触るのはこのクラスだけに閉じる。よって Core 側は
    /// 「データ形式を知らない」「UnityEngine を参照しない」状態を保てる。
    /// 合成ルート（Seed.App）は
    /// <code>
    ///   var data = catalog.Build();
    ///   var loader = new MasterDataLoader();
    ///   loader.RegisterAll(data, registry);
    ///   loader.BindFactories&lt;SkillDefinition, Hunter, LogicEventHandlerBase&gt;(
    ///       data, skillFactories, (def, hunter) => new AttackBoostHandler(hunter, def.rate));
    /// </code>
    /// のように、データを読む→登録する→振る舞いを紐付ける、の3手で構成できる。
    ///
    /// [なぜ static でなくインスタンスか] 収集用バッファを保持して呼び出しごとの確保を避けるため
    /// （低GC規約）。合成ルートで1つ作って使い回す想定。スレッド共有はしない。
    /// </summary>
    public sealed class MasterDataLoader
    {
        /// <summary>ID昇順の収集バッファ（再利用して毎回の確保を避ける）。</summary>
        private readonly List<IEntityDefinition> _buffer = new List<IEntityDefinition>();

        /// <summary>
        /// 全定義を EntityRegistry へ「データが持つID」で登録する。
        ///
        /// - 事前に ValidateGlobalIdUniqueness を通す。型をまたいだID衝突は
        ///   RegisterWithId でも弾かれるが、そこまで進むと「どの2件が衝突したか」が分からない。
        ///   先に検証して原因の型名・DebugName を含む例外にする（構成ミスは早期・具体的に落とす規約）。
        /// - 登録はID昇順の決定的な順序で行う。EntityRegistry 自体は明示ID登録なので
        ///   順序に意味は無いが、「実行ごとに順序が揺れる」状態を残すと
        ///   トレースログの差分比較やバグ再現が壊れるため、順序も固定する。
        /// - 同じ MasterDataSet を二度渡しても RegisterWithId が同一IDを返すだけで無害（冪等）。
        ///   別のエンティティが同IDを取っていれば LogicException で落ちる（既存の重複ガードがそのまま活きる）。
        /// </summary>
        public void RegisterAll(MasterDataSet data, EntityRegistry registry)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }
            if (registry == null)
            {
                throw new ArgumentNullException(nameof(registry));
            }

            data.ValidateGlobalIdUniqueness();
            data.CollectAllOrderedById(_buffer);

            for (var i = 0; i < _buffer.Count; i++)
            {
                var definition = _buffer[i];
                registry.RegisterWithId(definition, definition.Id);
            }

            // バッファに定義（＝ScriptableObject の場合もある）の参照を残さない。
            _buffer.Clear();
        }

        /// <summary>
        /// 定義型ごとに「ID → 生成方法」を FactoryRegistry へ紐付ける。
        ///
        /// [実APIに合わせた形] Seed.Core の FactoryRegistry は
        /// <c>FactoryRegistry&lt;TKey, TArg, TProduct&gt;</c> というジェネリック型で、非ジェネリックの
        /// 「FactoryRegistry」型は存在しない。よってキー型を
        /// <c>int</c>（＝定義ID＝EntityRegistry と同じID体系）に固定した形で受け取る。
        /// キーをIDに揃えるのは、レコードに載るのがIDだけであり、
        /// 「レコードのID → その場で生成すべき振る舞い」を追加の対応表なしに辿れるようにするため。
        ///
        /// [呼び出し例]
        /// <code>
        ///   loader.BindFactories&lt;SkillDefinition, Hunter, LogicEventHandlerBase&gt;(
        ///       data, skillFactories, (def, hunter) => new AttackBoostHandler(hunter, def.ratePermille));
        /// </code>
        /// （型引数は3つとも明示する。C#はラムダの引数型から TDefinition を推論できないため）
        ///
        /// [GC] 定義1件につきクロージャ1つを確保する。ロード時に1回だけ走る処理であり、
        /// 実行中（毎フレーム）には確保が発生しない設計。
        /// [二重バインド] 既定では FactoryRegistry.Bind が構成ミスとして例外にする。
        /// 「共通データを読んだ後にタイトル別で差し替える」意図があるときだけ allowOverwrite: true。
        /// </summary>
        public void BindFactories<TDefinition, TArg, TProduct>(
            MasterDataSet data,
            FactoryRegistry<int, TArg, TProduct> factories,
            Func<TDefinition, TArg, TProduct> factory,
            bool allowOverwrite = false)
            where TDefinition : class, IEntityDefinition
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }
            if (factories == null)
            {
                throw new ArgumentNullException(nameof(factories));
            }
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            var definitions = data.GetAll<TDefinition>();
            if (definitions.Count == 0)
            {
                // 「その型のデータが1件も無い」は取り込み漏れの典型。黙って0件成功にしない。
                throw new LogicException(
                    $"{typeof(TDefinition).Name} の定義が0件（カタログ/JSONの取り込み漏れ）。"
                    + "その仕様をタイトルから外す意図なら BindFactories 自体を呼ばないこと");
            }

            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                factories.Bind(definition.Id, arg => factory(definition, arg), allowOverwrite);
            }
        }
    }
}
