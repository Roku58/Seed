using System;
using UnityEngine;

namespace Seed.Motion
{
    /// <summary>
    /// 揺れものリグ（髪・尻尾・マント・アクセサリ）。アニメの上へ LateUpdate で重ねる「艶」。
    ///
    /// [使い方] 根本→先端のボーン列を渡し、MotionRig.With で装着するだけ。
    /// めり込み防止は球コライダー（中心Transform＋半径）を任意個。
    /// 書き戻しは回転差分のみ（位置を直書きしない）＝スキニングモデルでも破綻しない。
    ///
    /// [順序] MotionRig の登録順に適用されるため、注視・IKの後に装着すると
    /// 頭や腕の最終的な動きへ揺れが追従する（順序が演出の意味を持つ）。
    /// 真実（ActorPose・行動状態）には一切書き込まない。
    ///
    /// [前提] 揺れボーン列はアニメーションクリップでは焼かない（揺れの真実はシミュレーション）。
    /// 素姿勢は装着時のローカル回転で定義し、毎フレーム復元してから採取する——
    /// 前フレームの自己出力を復元目標に読むと、髪型が崩れて垂れ切るフィードバックになるため。
    /// </summary>
    public sealed class SpringBoneRig : IPoseRig
    {
        /// <summary>ボーン列（根本→先端）。</summary>
        private readonly Transform[] _bones;

        /// <summary>シミュレーション本体（純C#）。</summary>
        private readonly SpringBoneChain _chain;

        /// <summary>押し出し球（中心Transformは毎フレーム追跡する）。</summary>
        private readonly (Transform Center, float Radius)[] _colliders;

        /// <summary>球のワールド値バッファ（毎フレームのGC割り当てゼロ）。</summary>
        private readonly SpringSphere[] _sphereBuffer;

        /// <summary>アニメ素姿勢のバッファ（同上）。</summary>
        private readonly Vector3[] _animated;

        /// <summary>テレポート判定の閾値（2乗）。</summary>
        private readonly float _teleportThresholdSqr;

        /// <summary>装着時の各ボーンのローカル回転（素姿勢の定義。毎フレーム復元する）。</summary>
        private readonly Quaternion[] _initialLocalRotations;

        /// <summary>最後に Apply したフレーム番号（適用が跳んでいたら連続性を諦める）。</summary>
        private int _lastAppliedFrame = int.MinValue;

        /// <summary>前フレームの根本位置（テレポート検出用）。</summary>
        private Vector3 _lastRootPosition;

        /// <summary>
        /// SpringBoneRig を生成する。根本が teleportThreshold を超えて瞬間移動したフレームは
        /// 自動でアニメ姿勢へリセットする（ワープ後に髪が鞭のように吹き飛ぶのを防ぐ）。
        /// </summary>
        public SpringBoneRig(Transform[] bones, SpringBoneParams? parameters = null,
            (Transform Center, float Radius)[] colliders = null, float teleportThreshold = 2f)
        {
            if (bones == null || bones.Length < 2)
            {
                throw new ArgumentException("揺れものリグには2本以上のボーン列が必要です。");
            }
            _bones = bones;
            _animated = new Vector3[bones.Length];
            _initialLocalRotations = new Quaternion[bones.Length];
            for (var i = 0; i < bones.Length; i++)
            {
                _animated[i] = bones[i].position;
                _initialLocalRotations[i] = bones[i].localRotation;
            }
            _chain = new SpringBoneChain(_animated, parameters ?? SpringBoneParams.Default);
            _colliders = colliders ?? Array.Empty<(Transform, float)>();
            _sphereBuffer = new SpringSphere[_colliders.Length];
            _teleportThresholdSqr = teleportThreshold * teleportThreshold;
            _lastRootPosition = _animated[0];
        }

        /// <summary>適用率（0〜1）。0でもシミュレーションは進める（再開時に不連続にならない）。</summary>
        public float Weight { get; set; } = 1f;

        /// <summary>外力（風など）。演出の方針として App が毎フレーム設定してよい。</summary>
        public Vector3 ExternalForce
        {
            get => _chain.ExternalForce;
            set => _chain.ExternalForce = value;
        }

        /// <summary>揺れをアニメ後の姿勢へ重ねる（MotionRig が LateUpdate で呼ぶ）。</summary>
        public void Apply()
        {
            // 素姿勢を復元してから採取する（根本→先端の順なので、位置の読みは常に
            // 復元済みの先祖を反映する）。前フレームの自己出力を目標に読まないための要
            for (var i = 0; i < _bones.Length; i++)
            {
                _bones[i].localRotation = _initialLocalRotations[i];
                _animated[i] = _bones[i].position;
            }

            // ワープ・ステージ切替・無効化からの復帰では追従リセット（鞭化防止）
            var frame = Time.frameCount;
            if (frame > _lastAppliedFrame + 1
                || (_animated[0] - _lastRootPosition).sqrMagnitude > _teleportThresholdSqr)
            {
                _chain.Reset(_animated);
            }
            _lastAppliedFrame = frame;
            _lastRootPosition = _animated[0];

            for (var c = 0; c < _colliders.Length; c++)
            {
                _sphereBuffer[c] = new SpringSphere(_colliders[c].Center.position, _colliders[c].Radius);
            }

            _chain.Step(Time.deltaTime, _animated, _sphereBuffer, _sphereBuffer.Length);

            if (Weight <= 0f)
            {
                return;
            }

            // 回転差分で書き戻す（親から順に。先祖の回転が反映された現在位置を基準にする）
            for (var i = 0; i < _bones.Length - 1; i++)
            {
                var bonePosition = _bones[i].position;
                var currentDirection = _bones[i + 1].position - bonePosition;
                var solvedDirection = _chain.GetPosition(i + 1) - bonePosition;
                if (currentDirection.sqrMagnitude < 1e-12f || solvedDirection.sqrMagnitude < 1e-12f)
                {
                    continue;
                }
                var solved = Quaternion.FromToRotation(currentDirection, solvedDirection)
                    * _bones[i].rotation;
                _bones[i].rotation = Weight < 1f
                    ? Quaternion.Slerp(_bones[i].rotation, solved, Weight)
                    : solved;
            }
        }
    }
}
