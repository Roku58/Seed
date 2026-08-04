using System;
using UnityEngine;

namespace Seed.Motion
{
    /// <summary>
    /// 揺れものの調整値（髪・尻尾・マント等の「質感」をデータで決める）。
    /// </summary>
    public struct SpringBoneParams
    {
        /// <summary>復元の強さ（アニメ姿勢の向きへ戻る力。大きいほど硬い＝短髪向き）。</summary>
        public float Stiffness;

        /// <summary>減衰（0〜1）。0で揺れが残り続け、1で即座に止まる。</summary>
        public float Drag;

        /// <summary>重力（ワールド）。垂れ下がりの強さ。マントは強め・アホ毛は弱め。</summary>
        public Vector3 Gravity;

        /// <summary>関節の太さ（球コライダー押し出しに足される半径）。</summary>
        public float JointRadius;

        /// <summary>髪向けの無難な既定値。</summary>
        public static SpringBoneParams Default => new SpringBoneParams
        {
            Stiffness = 40f,
            Drag = 0.2f,
            Gravity = new Vector3(0f, -2f, 0f),
            JointRadius = 0.05f,
        };
    }

    /// <summary>押し出し用の球（頭・胴に髪がめり込むのを防ぐ）。</summary>
    public readonly struct SpringSphere
    {
        /// <summary>中心（ワールド）。</summary>
        public readonly Vector3 Center;

        /// <summary>半径。</summary>
        public readonly float Radius;

        /// <summary>SpringSphere を生成する。</summary>
        public SpringSphere(Vector3 center, float radius)
        {
            Center = center;
            Radius = radius;
        }
    }

    /// <summary>
    /// 揺れものチェーンのシミュレーション（純C#・Verlet積分＋長さ拘束）。
    ///
    /// [仕組み] 根本はアニメ姿勢へ常に追従する（「運び手」はアニメ側）。各関節は
    /// 慣性（前ステップからの速度×減衰）＋復元（アニメ姿勢の向きへ）＋重力・外力で動き、
    /// 最後に「親からボーン長ちょうど」の球面へ射影する——伸び縮みしない髪・尻尾になる。
    ///
    /// [なぜ物理エンジンを使わないか] Rigidbody/Cloth は演出用途には過剰で調整しづらく、
    /// 実行順・決定性も制御できない。本実装は Transform に触れない純数学なので
    /// EditMode で質感・安定性を数値検証でき、同じ入力列なら結果が完全一致する。
    ///
    /// [安定性] 時間は内部の貯金（アキュムレータ）へ貯め、常に厳密な固定刻み（1/60秒）だけ
    /// 前進して端数を次回へ繰り越す。刻み幅が完全に一定なので力の効き方がFPSへ依存せず、
    /// 1回で消化する量に上限もあるためヒッチ（巨大なdt）でも爆発しない。
    /// </summary>
    public sealed class SpringBoneChain
    {
        /// <summary>積分の固定刻み（秒）。常に厳密にこの幅で前進する＝FPS非依存の質感。</summary>
        private const float FixedStepSeconds = 1f / 60f;

        /// <summary>1回の Step で消化する時間の上限（秒）。ヒッチで爆発させない安全弁。</summary>
        private const float MaxFrameSeconds = 0.1f;

        /// <summary>押し出しと長さ拘束を両立させる反復回数。</summary>
        private const int CollisionIterations = 4;

        /// <summary>現在位置（ワールド）。[0]は根本＝アニメ追従。</summary>
        private readonly Vector3[] _current;

        /// <summary>前ステップ位置（Verletの速度源）。</summary>
        private readonly Vector3[] _previous;

        /// <summary>各節の長さ（生成時の姿勢から確定。以後不変）。</summary>
        private readonly float[] _lengths;

        /// <summary>調整値。</summary>
        private readonly SpringBoneParams _parameters;

        /// <summary>未消化の時間の貯金（固定刻みに満たない端数の繰り越し）。</summary>
        private float _accumulator;

        /// <summary>SpringBoneChain を生成する（initialPositions の間隔がボーン長になる）。</summary>
        public SpringBoneChain(Vector3[] initialPositions, SpringBoneParams parameters)
        {
            if (initialPositions == null || initialPositions.Length < 2)
            {
                throw new ArgumentException("揺れものチェーンには2点以上の関節が必要です。");
            }
            _parameters = parameters;
            _current = (Vector3[])initialPositions.Clone();
            _previous = (Vector3[])initialPositions.Clone();
            _lengths = new float[initialPositions.Length];
            for (var i = 1; i < initialPositions.Length; i++)
            {
                _lengths[i] = Vector3.Distance(initialPositions[i - 1], initialPositions[i]);
            }
        }

        /// <summary>関節数。</summary>
        public int Count => _current.Length;

        /// <summary>外力（風など）。消費側が毎フレーム設定してよい（演出の方針はApp）。</summary>
        public Vector3 ExternalForce { get; set; }

        /// <summary>関節の現在位置（ワールド）。</summary>
        public Vector3 GetPosition(int index)
        {
            return _current[index];
        }

        /// <summary>アニメ姿勢へ即時一致させる（ワープ・ステージ切替時の鞭化防止）。</summary>
        public void Reset(Vector3[] animatedPositions)
        {
            for (var i = 0; i < _current.Length; i++)
            {
                _current[i] = animatedPositions[i];
                _previous[i] = animatedPositions[i];
            }
            _accumulator = 0f; // 貯金も捨てる（リセット前の古い時間で前進しない）
        }

        /// <summary>
        /// シミュレーションを進める。animatedPositions はアニメだけを適用した素の関節位置
        /// （復元の目標）。colliders は押し出し球（colliderCount 省略時は配列全長）。
        /// 時間は内部へ貯めて固定刻みで消化する（固定刻み未満の呼び出しでは前進しないことがある）。
        /// </summary>
        public void Step(float deltaSeconds, Vector3[] animatedPositions,
            SpringSphere[] colliders = null, int colliderCount = -1)
        {
            if (deltaSeconds <= 0f)
            {
                return;
            }
            if (animatedPositions.Length != _current.Length)
            {
                throw new ArgumentException("アニメ姿勢の関節数がチェーンと一致しません。");
            }
            // 固定刻みだけ前進し端数は繰り越す（刻み幅を可変にすると力の効き方がFPS依存になる）
            _accumulator = Mathf.Min(_accumulator + deltaSeconds, MaxFrameSeconds);
            var count = colliderCount >= 0 ? colliderCount : (colliders?.Length ?? 0);
            while (_accumulator >= FixedStepSeconds)
            {
                _accumulator -= FixedStepSeconds;
                StepOnce(FixedStepSeconds, animatedPositions, colliders, count);
            }
        }

        /// <summary>1サブステップ前進する（Verlet→長さ拘束→押し出し）。</summary>
        private void StepOnce(float h, Vector3[] animated, SpringSphere[] colliders, int colliderCount)
        {
            _previous[0] = _current[0];
            _current[0] = animated[0];
            for (var i = 1; i < _current.Length; i++)
            {
                // 復元の目標方向＝アニメ姿勢での親→子の向き
                var restDirection = animated[i] - animated[i - 1];
                restDirection = restDirection.sqrMagnitude > 1e-12f
                    ? restDirection.normalized
                    : Vector3.down;

                var next = _current[i]
                    + (_current[i] - _previous[i]) * (1f - _parameters.Drag)
                    + restDirection * (_parameters.Stiffness * h)
                    + (_parameters.Gravity + ExternalForce) * h;

                next = ConstrainToParent(next, _current[i - 1], _lengths[i]);

                // 押し出しと長さ拘束は互いを壊し合うので、数回反復して両立させる
                for (var iteration = 0; iteration < CollisionIterations && colliderCount > 0; iteration++)
                {
                    var pushed = false;
                    for (var c = 0; c < colliderCount; c++)
                    {
                        var offset = next - colliders[c].Center;
                        var pushRadius = colliders[c].Radius + _parameters.JointRadius;
                        if (offset.sqrMagnitude < pushRadius * pushRadius)
                        {
                            var direction = offset.sqrMagnitude > 1e-12f
                                ? offset.normalized
                                : Vector3.up;
                            next = colliders[c].Center + direction * pushRadius;
                            pushed = true;
                        }
                    }
                    if (!pushed)
                    {
                        break;
                    }
                    next = ConstrainToParent(next, _current[i - 1], _lengths[i]);
                }

                _previous[i] = _current[i];
                _current[i] = next;
            }
        }

        /// <summary>親からボーン長ちょうどの位置へ射影する（伸び縮み禁止）。</summary>
        private static Vector3 ConstrainToParent(Vector3 position, Vector3 parent, float length)
        {
            var offset = position - parent;
            return offset.sqrMagnitude > 1e-12f
                ? parent + offset.normalized * length
                : parent + Vector3.down * length;
        }
    }
}
