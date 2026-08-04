using System;
using System.Collections.Generic;

namespace Seed.Core.Presenter
{
    /// <summary>
    /// 再生キューを「チャンネル」（UI / Camera / Character など int キー）ごとに束ねる部品。
    ///
    /// [なぜ必要か] 1本の直列キューだけでは「UIのダメージ数字」「カメラ寄せ」「キャラの被弾」を
    /// 同時進行させられない。並行させたい単位ごとにキューを分け、Tick を一括で回し、
    /// 「全チャンネルがアイドルになったら入力を戻す」を1か所で判定できるようにする。
    /// （1レコードから複数演出を同時に出したいだけなら CompositeStep で足りる。
    ///   チャンネルは「系統ごとに独立した再生ペースを持たせたい」ときに使う）
    ///
    /// - チャンネルIDは各ゲームが enum を int にキャストして使う想定（コアは意味を持たない）
    /// - 純C#・低GC（チャンネル数は数個なので Dictionary ではなく並列 List ＋ for で線形探索）
    /// </summary>
    public sealed class PlaybackChannels<TRecord> where TRecord : struct
    {
        /// <summary>チャンネルID（_queues と同じ並び）。</summary>
        private readonly List<int> _ids;
        /// <summary>チャンネルごとの再生キュー（_ids と同じ並び）。</summary>
        private readonly List<RecordPlaybackQueue<TRecord>> _queues;

        /// <summary>PlaybackChannels を生成する。capacity は想定チャンネル数。</summary>
        public PlaybackChannels(int capacity = 4)
        {
            _ids = new List<int>(capacity);
            _queues = new List<RecordPlaybackQueue<TRecord>>(capacity);
        }

        /// <summary>登録済みチャンネル数。</summary>
        public int Count => _queues.Count;

        /// <summary>全チャンネルが再生を終えているか（1つでも動いていれば false）。</summary>
        public bool IsIdle
        {
            get
            {
                for (var i = 0; i < _queues.Count; i++)
                {
                    if (!_queues[i].IsIdle)
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        /// <summary>チャンネルを登録する。同じIDの二重登録は設計バグとして落とす。</summary>
        public void Add(int channelId, RecordPlaybackQueue<TRecord> queue)
        {
            if (queue == null)
            {
                throw new ArgumentNullException(nameof(queue));
            }
            if (IndexOf(channelId) >= 0)
            {
                throw new ArgumentException($"チャンネル {channelId} は既に登録済み");
            }

            _ids.Add(channelId);
            _queues.Add(queue);
        }

        /// <summary>チャンネルのキューを取得する。未登録なら null。</summary>
        public RecordPlaybackQueue<TRecord> Get(int channelId)
        {
            var index = IndexOf(channelId);
            return index < 0 ? null : _queues[index];
        }

        /// <summary>チャンネルのキューを取得する（未登録でも例外にしたくない場合）。</summary>
        public bool TryGet(int channelId, out RecordPlaybackQueue<TRecord> queue)
        {
            var index = IndexOf(channelId);
            if (index < 0)
            {
                queue = null;
                return false;
            }
            queue = _queues[index];
            return true;
        }

        /// <summary>並び順でチャンネルのキューを取得する（列挙用。foreachの割り当てを避けるため添字方式）。</summary>
        public RecordPlaybackQueue<TRecord> QueueAt(int index) => _queues[index];

        /// <summary>並び順でチャンネルIDを取得する。</summary>
        public int ChannelIdAt(int index) => _ids[index];

        /// <summary>全チャンネルを同じ経過時間で進める。</summary>
        public void Tick(float deltaSeconds)
        {
            for (var i = 0; i < _queues.Count; i++)
            {
                _queues[i].Tick(deltaSeconds);
            }
        }

        /// <summary>全チャンネルを早回しで消化する（スキップボタン用）。</summary>
        public void SkipAll()
        {
            for (var i = 0; i < _queues.Count; i++)
            {
                _queues[i].SkipAll();
            }
        }

        /// <summary>全チャンネルの残り演出を終端処理なしで捨てる（画面作り直し用）。</summary>
        public void DiscardAll()
        {
            for (var i = 0; i < _queues.Count; i++)
            {
                _queues[i].DiscardAll();
            }
        }

        /// <summary>全チャンネルの一時停止を切り替える（ポーズメニュー用）。</summary>
        public void SetPaused(bool paused)
        {
            for (var i = 0; i < _queues.Count; i++)
            {
                _queues[i].IsPaused = paused;
            }
        }

        /// <summary>全チャンネルの再生速度をそろえる（倍速ボタン用）。</summary>
        public void SetSpeedMultiplier(float multiplier)
        {
            for (var i = 0; i < _queues.Count; i++)
            {
                _queues[i].SpeedMultiplier = multiplier;
            }
        }

        /// <summary>チャンネルIDから並び順を引く（チャンネル数は数個なので線形探索で十分）。</summary>
        private int IndexOf(int channelId)
        {
            for (var i = 0; i < _ids.Count; i++)
            {
                if (_ids[i] == channelId)
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
