// ============================================================================
// 【サンプルコード】Sample_HitboxTrigger3D
// 攻撃判定用のトリガーコライダー。
//
// ★パターン: 「当たったか」はアクション層（物理）の責務。
// このコンポーネントは有効フレーム中の接触から Sample_PartMarker3D を拾い、
// 「どの部位に当たったか」をコールバックで通知するだけで、ダメージ計算は行わない
// （計算は受け取った側が HitResolutionSection に依頼する）。
// 多段ヒット防止（同じ振りで同じ部位に1回だけ）もアクション層のここで行う。
// ============================================================================

using System;
using System.Collections.Generic;
using Seed.Core.Samples.ActionBattle;
using UnityEngine;

namespace Seed.Core.Samples
{
    /// <summary>【サンプル】有効フレーム制御と多段ヒット防止つきの攻撃トリガー。</summary>
    public sealed class Sample_HitboxTrigger3D : MonoBehaviour
    {
        /// <summary>部位へ接触したときの通知先。</summary>
        private Action<Sample_MonsterPart> _onHitPart;

        /// <summary>この振りで既に当てた部位（多段ヒット防止）。</summary>
        private readonly HashSet<Sample_MonsterPart> _alreadyHit = new HashSet<Sample_MonsterPart>();

        /// <summary>判定コライダー。</summary>
        private Collider _collider;

        /// <summary>通知先を設定する（生成直後に一度だけ呼ぶ）。</summary>
        public void Initialize(Action<Sample_MonsterPart> onHitPart)
        {
            _onHitPart = onHitPart;
            _collider = GetComponent<Collider>();
            _collider.enabled = false;
        }

        /// <summary>攻撃の有効フレーム開始（振りごとにヒット済みリストをリセット）。</summary>
        public void Activate()
        {
            _alreadyHit.Clear();
            _collider.enabled = true;
        }

        /// <summary>攻撃の有効フレーム終了。</summary>
        public void Deactivate()
        {
            _collider.enabled = false;
        }

        /// <summary>接触した相手から部位マーカーを拾い、未ヒットの部位なら通知する。</summary>
        private void OnTriggerEnter(Collider other)
        {
            var marker = other.GetComponent<Sample_PartMarker3D>();
            if (marker == null)
            {
                return;
            }
            if (!_alreadyHit.Add(marker.Part))
            {
                return; // 同じ振りで同じ部位には1回だけ
            }
            _onHitPart?.Invoke(marker.Part);
        }
    }
}
