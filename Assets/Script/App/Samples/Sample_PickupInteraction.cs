using System;
using System.Collections.Generic;
using UnityEngine;

namespace Seed.App
{
    /// <summary>
    /// 【サンプル】「物を拾う」インタラクション（腕IKの実演）。
    ///
    /// [役割] 拾えるアイテムの台帳と、拾う動作の状態機械
    /// 「伸ばす（Reaching）→ 掴む（Grabbing）→ 戻す（Recovering）」を持つ。
    /// **進行（状態）は Tick で刻み、腕IKへの反映（艶）は呼び出し側が
    /// <see cref="Weight"/> と <see cref="TargetPoint"/> を読んで行う**——
    /// 「状態はTick、艶はUpdate」の規約どおり、このクラスはリグを直接触らない。
    ///
    /// [流れ] TryStart で手が届く範囲の最寄りアイテムを選ぶ → 重みが滑らかに
    /// 立ち上がる（smoothstep）→ 伸ばし切ったらアイテムを手に吸着させて縮小 →
    /// 腕を戻して完了。途中で走り出す・アイテムが消える等の中断は、
    /// その時点の重みから滑らかに 0 へ戻す（腕が跳ねない）。
    /// </summary>
    public sealed class Sample_PickupInteraction
    {
        /// <summary>拾う動作の段階。</summary>
        private enum Phase
        {
            /// <summary>待機。</summary>
            Idle,
            /// <summary>手を伸ばしている（重み 0→1）。</summary>
            Reaching,
            /// <summary>掴んでいる（アイテムが手に付いて縮む）。</summary>
            Grabbing,
            /// <summary>腕を戻している（重み →0）。</summary>
            Recovering,
        }

        /// <summary>まだ拾われていないアイテム。</summary>
        private readonly List<Transform> _items = new List<Transform>();

        /// <summary>伸ばし切るまでの秒数。</summary>
        private readonly float _reachSeconds;

        /// <summary>掴んで収める秒数。</summary>
        private readonly float _grabSeconds;

        /// <summary>腕を戻す秒数。</summary>
        private readonly float _recoverSeconds;

        /// <summary>現在の段階。</summary>
        private Phase _phase = Phase.Idle;

        /// <summary>段階内の進行 0〜1。</summary>
        private float _progress;

        /// <summary>拾おうとしているアイテム。</summary>
        private Transform _active;

        /// <summary>掴んだ瞬間のアイテムの大きさ（縮小の始点）。</summary>
        private Vector3 _grabScale;

        /// <summary>中断時の重み（そこから滑らかに 0 へ戻す）。</summary>
        private float _recoverFrom;

        /// <summary>掴んだ瞬間の手から見たアイテム位置（手の中へ引き寄せる始点）。</summary>
        private Vector3 _grabLocalPosition;

        /// <summary>今回の戻し時間（伸ばし量に比例。BeginRecover が決める）。</summary>
        private float _recoverDuration;

        /// <summary>Sample_PickupInteraction を生成する。</summary>
        public Sample_PickupInteraction(float startRange = 1.6f, float reachSeconds = 0.4f,
            float grabSeconds = 0.25f, float recoverSeconds = 0.35f)
        {
            StartRange = startRange;
            _reachSeconds = reachSeconds;
            _grabSeconds = grabSeconds;
            _recoverSeconds = recoverSeconds;
        }

        /// <summary>拾い始められる距離（m）。オーブの接近パルスの判定にも使う。</summary>
        public float StartRange { get; }

        /// <summary>まだ拾われていないアイテム（浮遊アニメの対象）。</summary>
        public IReadOnlyList<Transform> Items => _items;

        /// <summary>拾い終えた個数。</summary>
        public int PickedCount { get; private set; }

        /// <summary>登録された総数。</summary>
        public int TotalCount { get; private set; }

        /// <summary>動作中か（待機以外）。</summary>
        public bool IsBusy => _phase != Phase.Idle;

        /// <summary>腕IKへ渡す目標が有効か。</summary>
        public bool HasTarget => _phase != Phase.Idle;

        /// <summary>腕IKへ渡す重み 0〜1。</summary>
        public float Weight { get; private set; }

        /// <summary>腕IKへ渡す目標位置（中断後も最後の位置を保つ＝腕が跳ねない）。</summary>
        public Vector3 TargetPoint { get; private set; }

        /// <summary>1個拾い終えた通知（拾った数, 総数）。</summary>
        public event Action<int, int> PickedUp;

        /// <summary>アイテムを台帳へ登録する。</summary>
        public void AddItem(Transform item)
        {
            if (item == null || _items.Contains(item))
            {
                return;
            }
            _items.Add(item);
            TotalCount++;
        }

        /// <summary>
        /// 手が届く範囲の最寄りアイテムへ拾う動作を始める（動作中・範囲内に無い場合は false）。
        /// </summary>
        public bool TryStart(Vector3 playerPosition)
        {
            if (_phase != Phase.Idle)
            {
                return false;
            }
            Transform nearest = null;
            var nearestDistance = StartRange;
            for (var i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                if (item == null || !item.gameObject.activeInHierarchy)
                {
                    continue;
                }
                var distance = Vector3.Distance(playerPosition, item.position);
                if (distance <= nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = item;
                }
            }
            if (nearest == null)
            {
                return false;
            }
            _active = nearest;
            _phase = Phase.Reaching;
            _progress = 0f;
            TargetPoint = nearest.position;
            return true;
        }

        /// <summary>動作を1歩進める（毎Tick呼ぶ。hand は吸着先＝プレイヤーの手ボーン）。</summary>
        public void Tick(float deltaTime, Vector3 playerPosition, float planarSpeed, Transform hand)
        {
            switch (_phase)
            {
                case Phase.Reaching:
                    TickReaching(deltaTime, playerPosition, planarSpeed, hand);
                    break;
                case Phase.Grabbing:
                    TickGrabbing(deltaTime);
                    break;
                case Phase.Recovering:
                    _progress += deltaTime / _recoverDuration;
                    Weight = Mathf.Lerp(_recoverFrom, 0f, Smooth01(_progress));
                    if (_progress >= 1f)
                    {
                        Weight = 0f;
                        _phase = Phase.Idle;
                    }
                    break;
            }
        }

        /// <summary>台帳・動作・実績を初期状態へ戻す（フェーズ退場時）。</summary>
        public void Clear()
        {
            if (_active != null && _phase == Phase.Grabbing)
            {
                _active.gameObject.SetActive(false); // 掴みかけを半端な大きさで残さない
            }
            _items.Clear();
            _active = null;
            _phase = Phase.Idle;
            Weight = 0f;
            PickedCount = 0;
            TotalCount = 0;
        }

        /// <summary>
        /// 動作を即座に畳む（決着など、徐々に戻す猶予が無い外部事情用）。
        /// 掴みかけのアイテムは拾い切った扱いで消す。腕の重みは即 0 になるため、
        /// 呼び出し側はリグの目標も外すこと（パイプライン停止後も LateUpdate は動く）。
        /// </summary>
        public void ForceFinish()
        {
            if (_phase == Phase.Grabbing && _active != null)
            {
                _active.gameObject.SetActive(false);
                PickedCount++;
                PickedUp?.Invoke(PickedCount, TotalCount);
            }
            _active = null;
            _phase = Phase.Idle;
            Weight = 0f;
        }

        /// <summary>原点回帰でワールドが delta ずれた時に目標点を追従させる。</summary>
        public void ShiftOrigin(Vector3 delta)
        {
            TargetPoint += delta;
        }

        /// <summary>手を伸ばす段階の進行。</summary>
        private void TickReaching(float deltaTime, Vector3 playerPosition, float planarSpeed,
            Transform hand)
        {
            // 中断条件: アイテム消滅・吸着先が無い・走り出した・離れすぎた
            var invalid = _active == null || !_active.gameObject.activeInHierarchy
                || hand == null || !hand.gameObject.activeInHierarchy
                || planarSpeed > 1.5f
                || Vector3.Distance(playerPosition, _active.position) > StartRange + 0.8f;
            if (invalid)
            {
                BeginRecover();
                return;
            }

            TargetPoint = _active.position;
            _progress += deltaTime / _reachSeconds;
            Weight = Smooth01(Mathf.Min(_progress, 1f));
            if (_progress >= 1f)
            {
                // 掴む: 以後は手に吸着して縮む。台帳からは即座に外す＝浮遊アニメの対象外にする
                _items.Remove(_active);
                _active.SetParent(hand, worldPositionStays: true);
                _grabScale = _active.localScale;
                _grabLocalPosition = _active.localPosition;
                _phase = Phase.Grabbing;
                _progress = 0f;
            }
        }

        /// <summary>掴む段階の進行（手の中へ引き寄せながら縮んで消える）。</summary>
        private void TickGrabbing(float deltaTime)
        {
            if (_active == null)
            {
                BeginRecover();
                return;
            }
            Weight = 1f;
            // TargetPoint は掴んだ瞬間の値で凍結する——アイテムはもう手の子であり、
            // 追い続けると「IKの目標＝IKが動かした手自身」の自己参照で腕が世界に釘付けになる
            _progress += deltaTime / _grabSeconds;
            var t = Mathf.Clamp01(_progress);
            _active.localScale = Vector3.Lerp(_grabScale, Vector3.zero, t);
            // 腕のリーチ外（クランプで手前停止）でも「手の中へ吸い込まれて消える」見た目にする
            _active.localPosition = Vector3.Lerp(_grabLocalPosition, Vector3.zero, t);
            if (_progress >= 1f)
            {
                _active.gameObject.SetActive(false);
                _active = null;
                PickedCount++;
                PickedUp?.Invoke(PickedCount, TotalCount);
                BeginRecover();
            }
        }

        /// <summary>現在の重みから滑らかに腕を戻し始める。</summary>
        private void BeginRecover()
        {
            _recoverFrom = Weight;
            _progress = 0f;
            if (_recoverFrom <= 0.001f)
            {
                // 伸ばす前の中断（走りながらの[2]等）は即待機へ——無反応な空ロックを作らない
                Weight = 0f;
                _phase = Phase.Idle;
                return;
            }
            // 戻し時間は伸ばし量に比例（少しだけ伸びた腕が長々と戻らない）
            _recoverDuration = Mathf.Max(_recoverSeconds * _recoverFrom, 0.05f);
            _phase = Phase.Recovering;
        }

        /// <summary>smoothstep（両端の速度が0＝腕の動き出し・止まりが柔らかい）。</summary>
        private static float Smooth01(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }
    }
}
