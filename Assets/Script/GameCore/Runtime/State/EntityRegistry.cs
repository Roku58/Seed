using System;
using System.Collections.Generic;

namespace Seed.Core
{
    /// <summary>
    /// エンティティ（アクター・部位など）に安定した整数IDを振る台帳。
    ///
    /// [目的] 事象レコードにオブジェクト参照を直接持たせると、
    /// セーブ・リプレイ保存・ログ送信ができない（参照は直列化できない）。
    /// レコードには本レジストリで振ったIDだけを入れ、
    /// 表示・再生時に GetEntity で引き直すのが推奨パターン。
    ///
    /// - IDは登録順に 1 から採番（0 は「なし」の予約値）
    /// - 同じオブジェクトを二度 Register しても同じIDを返す
    /// - LogicContext.AddExtension で登録して使う想定
    ///   （ISnapshotParticipant 実装なので巻き戻しの参加者に自動登録される）
    ///
    /// [退場と巻き戻し]
    /// - Unregister / UnregisterId で台帳から外せる（死んだエンティティへの強参照を残さない）
    /// - 空いたIDは再利用しない。採番は常に「末尾から」進める。
    ///   再利用すると、保存済みレコード・リプレイの中の同じIDが別のエンティティを指す
    ///   （＝再生時に別人へダメージが入るような、再現の難しい事故になる）
    /// - CaptureState/RestoreState は採番カーソルまで戻す。戻さないと
    ///   「巻き戻し後の再スポーンで別IDが振られ、記録と再生で分岐する」
    /// </summary>
    public sealed class EntityRegistry : ISnapshotParticipant
    {
        /// <summary>「対象なし」を表す予約ID。</summary>
        public const int None = 0;

        /// <summary>採番カーソル・台帳の内容を丸ごと持つ不透明トークン。</summary>
        private sealed class RegistryState
        {
            /// <summary>ID順のエンティティ（index 0 は None のため常に null）。</summary>
            public object[] Entities;
            /// <summary>次に自動採番するID。</summary>
            public int NextId;
        }

        /// <summary>エンティティ→IDの索引。</summary>
        private readonly Dictionary<object, int> _ids = new Dictionary<object, int>();
        private readonly List<object> _entities = new List<object> { null }; // index 0 = None
        /// <summary>次に自動採番するID（穴を再利用しないため、常に前進のみ）。</summary>
        private int _nextId = 1;

        /// <summary>登録済みエンティティ数（None を除く。Unregister した分は含まない）。</summary>
        public int Count => _ids.Count;

        /// <summary>これまでに払い出したIDの次の値（診断・テスト用の採番カーソル）。</summary>
        public int NextId => _nextId;

        /// <summary>エンティティを登録しIDを返す（自動採番）。登録済みなら既存IDを返す。</summary>
        public int Register(object entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            if (_ids.TryGetValue(entity, out var existing))
            {
                return existing;
            }

            // 明示ID登録や退場で空いた穴は飛ばし、常に末尾（採番カーソル）から採番する
            var id = _nextId;
            _nextId++;
            EnsureSlot(id);
            _entities[id] = entity;
            _ids.Add(entity, id);
            return id;
        }

        /// <summary>
        /// 明示IDで登録する（★登録順依存の根絶）。
        /// マスタデータのIDと揃えたいときはこちらを使う。合成ルートの登録順を変えても
        /// 保存済みリプレイ・セーブのIDがズレない。衝突（別エンティティが同IDを使用/
        /// 同エンティティが別ID）は構成ミスとして例外。
        /// </summary>
        public int RegisterWithId(object entity, int id)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }
            if (id <= None)
            {
                throw new ArgumentOutOfRangeException(nameof(id), "IDは1以上");
            }

            if (_ids.TryGetValue(entity, out var existing))
            {
                if (existing != id)
                {
                    throw new LogicException($"エンティティは既にID {existing} で登録済み（要求: {id}）");
                }
                return existing;
            }

            EnsureSlot(id);
            if (_entities[id] != null)
            {
                throw new LogicException($"ID {id} は別のエンティティが使用中");
            }

            _entities[id] = entity;
            _ids.Add(entity, id);
            if (id >= _nextId)
            {
                _nextId = id + 1; // 明示IDより後ろから自動採番する（既存IDと衝突させない）
            }
            return id;
        }

        /// <summary>
        /// エンティティを台帳から外す（退場・撃破後の後始末）。外せたら true。
        /// 空いたIDは再利用せず「欠番」として残す（過去のレコードが別人を指さないため）。
        /// </summary>
        public bool Unregister(object entity)
        {
            if (entity == null || !_ids.TryGetValue(entity, out var id))
            {
                return false;
            }
            _ids.Remove(entity);
            _entities[id] = null; // 強参照を切る（長期セッションでの死んだ参照の滞留防止）
            return true;
        }

        /// <summary>IDを指定して台帳から外す。外せたら true。</summary>
        public bool UnregisterId(int id)
        {
            if (id <= None || id >= _entities.Count)
            {
                return false;
            }
            var entity = _entities[id];
            if (entity == null)
            {
                return false;
            }
            _ids.Remove(entity);
            _entities[id] = null;
            return true;
        }

        /// <summary>IDを取得（未登録なら false）。</summary>
        public bool TryGetId(object entity, out int id)
        {
            if (entity != null && _ids.TryGetValue(entity, out id))
            {
                return true;
            }
            id = None;
            return false;
        }

        /// <summary>登録済みエンティティのID（未登録なら例外＝合成ルートの構成ミスを早期検知）。</summary>
        public int GetId(object entity)
        {
            if (TryGetId(entity, out var id))
            {
                return id;
            }
            throw new LogicException($"EntityRegistry に未登録: {entity}");
        }

        /// <summary>IDからエンティティを引く。None・範囲外・退場済みは null。</summary>
        public T GetEntity<T>(int id) where T : class
        {
            if (id <= None || id >= _entities.Count)
            {
                return null;
            }
            return _entities[id] as T;
        }

        // ---------------- ISnapshotParticipant ----------------

        /// <summary>
        /// 台帳の内容と採番カーソルを不透明トークンとして取り出す。
        /// 配列へ複製するので、以降の Register/Unregister はトークンに影響しない。
        /// </summary>
        public object CaptureState()
        {
            var entities = new object[_entities.Count];
            for (var i = 0; i < _entities.Count; i++)
            {
                entities[i] = _entities[i];
            }
            return new RegistryState { Entities = entities, NextId = _nextId };
        }

        /// <summary>トークンから台帳と採番カーソルを完全復元する（巻き戻し後の再スポーンで同じIDになる）。</summary>
        public void RestoreState(object state)
        {
            if (!(state is RegistryState registryState))
            {
                throw new LogicException("EntityRegistry のスナップショットではないトークンが渡された");
            }

            _ids.Clear();
            _entities.Clear();
            var entities = registryState.Entities;
            for (var i = 0; i < entities.Length; i++)
            {
                _entities.Add(entities[i]);
                if (i > None && entities[i] != null)
                {
                    _ids.Add(entities[i], i);
                }
            }
            _nextId = registryState.NextId;
        }

        /// <summary>指定IDの席を確保する（穴は null で埋める）。</summary>
        private void EnsureSlot(int id)
        {
            while (_entities.Count <= id)
            {
                _entities.Add(null);
            }
        }
    }
}
