using System;
using System.Collections;
using System.Collections.Generic;
using Seed.Core;

namespace Seed.Data
{
    /// <summary>
    /// 読み込み済みマスターデータの台帳（純C#）。「定義の型ごと」に「ID→定義」を保持し、検証する。
    ///
    /// [位置づけ] SO / JSON / コード直書きのどの入口から来た定義も、いったん本台帳に集約する。
    /// この先（Seed.Core への流し込み）は MasterDataLoader が担う。
    /// 本クラスが UnityEngine に一切依存しないのは意図的で、
    /// EditMode テストで MonoBehaviour も ScriptableObject も使わずに全規約を検証できるようにするため。
    ///
    /// [型キーの規約] 引き当ては「型の完全一致」で行い、継承関係は辿らない。
    /// 基底型で辿れるようにすると「複数の派生型に同じIDが居る」場合の解が曖昧になり、
    /// 探索コストも O(1) でなくなる。どの単位で束ねるかは Add の呼び出し側が明示的に決める規約。
    ///
    /// [ID重複の扱い] 同一型内の重複は Add が即例外。加えて
    /// ValidateGlobalIdUniqueness で「型をまたいだ重複」も検出できる（理由は当該メソッドのdoc参照）。
    /// 構成ミスは黙って上書きせず早期に落とす、が本プロジェクトの規約。
    /// </summary>
    public sealed class MasterDataSet
    {
        /// <summary>1つの定義型ぶんの保管箱（ID索引＋ID昇順リスト）。</summary>
        private sealed class Bucket
        {
            /// <summary>この箱が受け持つ型キー（例外メッセージ用に保持する）。</summary>
            public readonly Type DefinitionType;

            /// <summary>ID→定義の索引（O(1) 引き当て用）。</summary>
            public readonly Dictionary<int, IEntityDefinition> ById = new Dictionary<int, IEntityDefinition>();

            /// <summary>
            /// ID昇順に並べた定義列。挿入時に二分探索で位置を決めており、
            /// Add の呼び出し順に関係なく常に同じ並びになる（＝列挙・登録の決定性）。
            /// </summary>
            public readonly List<IEntityDefinition> Ordered = new List<IEntityDefinition>();

            /// <summary>GetAll が返す型付きビューのキャッシュ（呼ぶ度に確保しないため object で持つ）。</summary>
            public object CachedView;

            /// <summary>Bucket を生成する。</summary>
            public Bucket(Type definitionType)
            {
                DefinitionType = definitionType;
            }

            /// <summary>ID昇順を保ったまま挿入する（二分探索。LINQ・ソート再実行なしで並びを維持）。</summary>
            public void InsertOrderedById(IEntityDefinition definition)
            {
                var id = definition.Id;
                var lo = 0;
                var hi = Ordered.Count;
                while (lo < hi)
                {
                    var mid = lo + ((hi - lo) >> 1);
                    if (Ordered[mid].Id < id)
                    {
                        lo = mid + 1;
                    }
                    else
                    {
                        hi = mid;
                    }
                }
                Ordered.Insert(lo, definition);
            }
        }

        /// <summary>
        /// List&lt;IEntityDefinition&gt; を IReadOnlyList&lt;TDefinition&gt; として見せる読み取り専用ビュー。
        ///
        /// [なぜ必要か] 内部の保管はキャッシュ共有のため非ジェネリックに寄せている一方、
        /// 公開APIは型付きで返したい。IReadOnlyList&lt;T&gt; の共変性は「派生→基底」方向にしか効かないので、
        /// 逆向きに見せる薄いラッパを1つ挟む。Bucket 側でキャッシュするため確保は型ごとに1回だけ。
        /// [低GC注意] foreach は列挙子を確保する。ホットパスでは for + インデクサで回すこと。
        /// </summary>
        private sealed class CastView<TDefinition> : IReadOnlyList<TDefinition>
            where TDefinition : class, IEntityDefinition
        {
            /// <summary>元の並び（Bucket.Ordered をそのまま参照する＝追加は即反映される）。</summary>
            private readonly List<IEntityDefinition> _source;

            /// <summary>CastView を生成する。</summary>
            public CastView(List<IEntityDefinition> source)
            {
                _source = source;
            }

            /// <summary>件数。</summary>
            public int Count => _source.Count;

            /// <summary>ID昇順の index 番目（型キー完全一致で入れているのでキャストは常に成功する）。</summary>
            public TDefinition this[int index] => (TDefinition)_source[index];

            /// <summary>列挙子を返す（低GC規約上は for + インデクサを推奨）。</summary>
            public IEnumerator<TDefinition> GetEnumerator()
            {
                for (var i = 0; i < _source.Count; i++)
                {
                    yield return (TDefinition)_source[i];
                }
            }

            /// <summary>非ジェネリック列挙子（IEnumerable 実装のための転送）。</summary>
            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        /// <summary>ID昇順の比較子（毎回ラムダを確保しないよう単一インスタンスを共有する）。</summary>
        private sealed class IdAscendingComparer : IComparer<IEntityDefinition>
        {
            /// <summary>共有インスタンス。</summary>
            public static readonly IdAscendingComparer Instance = new IdAscendingComparer();

            /// <summary>IDの昇順で比較する。</summary>
            public int Compare(IEntityDefinition x, IEntityDefinition y)
            {
                return x.Id.CompareTo(y.Id);
            }
        }

        /// <summary>型キー→保管箱。</summary>
        private readonly Dictionary<Type, Bucket> _buckets = new Dictionary<Type, Bucket>();

        /// <summary>全型合計の件数（都度数え直さないよう Add で加算する）。</summary>
        private int _count;

        /// <summary>保持している定義の総数（全型の合計）。</summary>
        public int Count => _count;

        /// <summary>保持している定義型の数（＝束ねている種類数。構成ログ用）。</summary>
        public int TypeCount => _buckets.Count;

        /// <summary>
        /// 定義を追加する。型キーは typeof(TDefinition)＝呼び出し側が「束ねる単位」を明示する。
        /// 同一型内でIDが重複したら構成ミスとして即例外（黙って上書きすると
        /// 「どちらのデータが効いているか分からない」不具合になり、レコード解釈もズレる）。
        /// </summary>
        public void Add<TDefinition>(TDefinition definition) where TDefinition : class, IEntityDefinition
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }
            AddInternal(typeof(TDefinition), definition);
        }

        /// <summary>
        /// 実行時型を型キーにして追加する。
        ///
        /// [なぜ Add&lt;T&gt; と別口が必要か] Inspector に並べた <c>EntityDefinitionAsset[]</c> のように
        /// 「基底型の1本の配列に複数種類の定義が混在する」入口では、呼び出し側が typeof(T) を書けない。
        /// その場合に限り実行時型（definition.GetType()）で束ねる。
        /// 結果として GetAll&lt;具体型&gt;() で引ける形になり、型キー完全一致の規約と整合する。
        /// </summary>
        public void AddByRuntimeType(IEntityDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }
            AddInternal(definition.GetType(), definition);
        }

        /// <summary>追加の実体（型キーの決め方だけが2つの公開APIで異なる）。</summary>
        private void AddInternal(Type definitionType, IEntityDefinition definition)
        {
            var id = definition.Id;
            if (id <= EntityRegistry.None)
            {
                // EntityRegistry.None(=0) は「対象なし」の予約値。RegisterWithId も1以上しか受けない。
                throw new LogicException(
                    $"定義 {definitionType.Name} のIDが不正: {id}（1以上。0 は EntityRegistry.None の予約値）");
            }

            if (!_buckets.TryGetValue(definitionType, out var bucket))
            {
                bucket = new Bucket(definitionType);
                _buckets.Add(definitionType, bucket);
            }

            if (bucket.ById.TryGetValue(id, out var existing))
            {
                if (ReferenceEquals(existing, definition))
                {
                    throw new LogicException(
                        $"定義 {definitionType.Name}#{id}（{definition.DebugName}）が二重登録された（カタログに同じ資産が2回並んでいる）");
                }
                throw new LogicException(
                    $"定義 {definitionType.Name} のID {id} が重複: 既存「{existing.DebugName}」/ 追加「{definition.DebugName}」");
            }

            bucket.ById.Add(id, definition);
            bucket.InsertOrderedById(definition);
            _count++;
        }

        /// <summary>IDから定義を引く（未登録・型キー不一致なら false）。</summary>
        public bool TryGet<TDefinition>(int id, out TDefinition definition)
            where TDefinition : class, IEntityDefinition
        {
            if (_buckets.TryGetValue(typeof(TDefinition), out var bucket)
                && bucket.ById.TryGetValue(id, out var found))
            {
                definition = found as TDefinition;
                return definition != null;
            }
            definition = null;
            return false;
        }

        /// <summary>
        /// IDから定義を引く。未登録は構成ミス（データ欠落・IDの打ち間違い）として例外。
        /// 「データが無いので既定値で動く」は不具合を隠すので採らない。
        /// </summary>
        public TDefinition Get<TDefinition>(int id) where TDefinition : class, IEntityDefinition
        {
            if (TryGet<TDefinition>(id, out var definition))
            {
                return definition;
            }
            throw new LogicException(
                $"マスターデータに未登録: {typeof(TDefinition).Name}#{id}（カタログ/JSONの取り込み漏れかIDの誤り）");
        }

        /// <summary>
        /// 指定型の定義を全件返す（常にID昇順。未登録の型なら空）。
        /// 返り値は内部リストのビューなので確保は型ごとに1回だけ。書き換えは不可。
        /// </summary>
        public IReadOnlyList<TDefinition> GetAll<TDefinition>() where TDefinition : class, IEntityDefinition
        {
            if (!_buckets.TryGetValue(typeof(TDefinition), out var bucket))
            {
                return Array.Empty<TDefinition>();
            }
            if (bucket.CachedView == null)
            {
                bucket.CachedView = new CastView<TDefinition>(bucket.Ordered);
            }
            return (IReadOnlyList<TDefinition>)bucket.CachedView;
        }

        /// <summary>指定型の定義が1件でもあるかを返す（構成ログ・任意データの有無判定用）。</summary>
        public bool Contains<TDefinition>() where TDefinition : class, IEntityDefinition
        {
            return _buckets.ContainsKey(typeof(TDefinition));
        }

        /// <summary>
        /// 型をまたいだID重複を検証する（重複していれば例外）。
        ///
        /// [なぜ型が違ってもIDが衝突してはいけないのか]
        /// EntityRegistry のID空間は「全エンティティで1本」である。
        /// 武器#10 とモンスター#10 のように型が違えばデータ上は成立してしまうが、
        /// 両方を RegisterWithId すると後から登録した側が
        /// 「ID 10 は別のエンティティが使用中」で落ちる（＝実行時まで気付かない）。
        /// さらに悪いのは、片方だけ登録する構成だとレコードのID 10 が
        /// どちらを指すのか解釈がブレること。よって取り込み直後に全型横断で検証し、
        /// 「武器は1000番台、モンスターは2000番台」のような採番規約の破りを早期に落とす。
        /// [コスト] 検証用の辞書を1つ確保する。ロード時に1回だけ呼ぶ想定で、毎フレーム呼ぶAPIではない。
        /// </summary>
        public void ValidateGlobalIdUniqueness()
        {
            var owners = new Dictionary<int, Type>(_count);
            foreach (var pair in _buckets)
            {
                var bucket = pair.Value;
                var ordered = bucket.Ordered;
                for (var i = 0; i < ordered.Count; i++)
                {
                    var definition = ordered[i];
                    var id = definition.Id;
                    if (owners.TryGetValue(id, out var owner))
                    {
                        throw new LogicException(
                            $"ID {id} が型をまたいで重複: {owner.Name} と {bucket.DefinitionType.Name}"
                            + $"（{definition.DebugName}）。EntityRegistry のID空間は全エンティティで1本なので"
                            + "、型ごとに採番範囲を分けること");
                    }
                    owners.Add(id, bucket.DefinitionType);
                }
            }
        }

        /// <summary>
        /// 全型の定義をID昇順で buffer に詰める（buffer は呼び出し側が保持・再利用する＝低GC）。
        ///
        /// [なぜ昇順で返すのか] 型キーの辞書の列挙順は保証されない。
        /// そのまま EntityRegistry へ流すと「実行ごとに登録順が変わる」ことになり、
        /// 登録順に依存する不具合が再現しなくなる。IDで並べ替えて完全に決定的な順序にしてから渡す。
        /// </summary>
        public void CollectAllOrderedById(List<IEntityDefinition> buffer)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            buffer.Clear();
            foreach (var pair in _buckets)
            {
                var ordered = pair.Value.Ordered;
                for (var i = 0; i < ordered.Count; i++)
                {
                    buffer.Add(ordered[i]);
                }
            }
            buffer.Sort(IdAscendingComparer.Instance);
        }
    }
}
