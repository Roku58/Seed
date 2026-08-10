using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Seed.App.Tests
{
    /// <summary>
    /// キネマティック移動モーター（CapsuleCollider＋Rigidbody・自前 collide-and-slide）のテスト。
    /// 物理クエリ（キャスト・オーバーラップ）はEditModeでも静的コライダーに対して動くため、
    /// 地面・壁・段差を実際に置いて移動結果を検証する。
    /// </summary>
    public sealed class KinematicMotorTests
    {
        /// <summary>1歩ぶんの刻み（60fps相当）。</summary>
        private const float Step = 1f / 60f;

        /// <summary>テスト中に作った GameObject（後始末用）。</summary>
        private readonly List<GameObject> _spawned = new List<GameObject>();

        /// <summary>作ったオブジェクトを毎テスト後に破棄する。</summary>
        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }
            _spawned.Clear();
            Physics.SyncTransforms();
        }

        /// <summary>空中から始めても重力で地面へ降り、接地する。</summary>
        [Test]
        public void 重力で落ちて接地する()
        {
            CreateBox("Ground", new Vector3(0f, -0.5f, 0f), new Vector3(20f, 1f, 20f));
            var motor = CreateMotor(out _);

            var position = new Vector3(0f, 1f, 0f);
            for (var i = 0; i < 90; i++)
            {
                motor.PreTick(Step);
                position = motor.Move(position, Vector3.zero);
            }

            Assert.That(position.y, Is.EqualTo(0f).Within(0.08f), "足元が地面へ着く");
            Assert.That(motor.IsGrounded, Is.True);
        }

        /// <summary>壁の手前で止まり、斜め入力なら壁に沿って滑る。</summary>
        [Test]
        public void 壁で止まり斜めなら滑る()
        {
            CreateBox("Ground", new Vector3(0f, -0.5f, 0f), new Vector3(20f, 1f, 20f));
            CreateBox("Wall", new Vector3(1.5f, 1f, 0f), new Vector3(0.2f, 2f, 8f)); // 面は x=1.4
            var motor = CreateMotor(out _);

            var position = Settle(motor, Vector3.zero);
            for (var i = 0; i < 60; i++)
            {
                motor.PreTick(Step);
                position = motor.Move(position, new Vector3(0.05f, 0f, 0f));
            }
            Assert.That(position.x, Is.LessThan(1.15f), "壁（x=1.4）へ半径ぶん残して止まる");
            Assert.That(position.x, Is.GreaterThan(0.9f), "壁の直前までは進めている");

            var beforeZ = position.z;
            for (var i = 0; i < 40; i++)
            {
                motor.PreTick(Step);
                position = motor.Move(position, new Vector3(0.04f, 0f, 0.04f));
            }
            Assert.That(position.z - beforeZ, Is.GreaterThan(0.5f), "壁に沿って z 方向へ滑る");
        }

        /// <summary>蹴上げ0.18mの段差を歩いて乗り越えられる。</summary>
        [Test]
        public void 段差を登れる()
        {
            CreateBox("Ground", new Vector3(0f, -0.5f, 0f), new Vector3(20f, 1f, 20f));
            CreateBox("StepTop", new Vector3(1.5f, 0.09f, 0f), new Vector3(1.6f, 0.18f, 4f)); // 上面 y=0.18

            var motor = CreateMotor(out _);

            var position = Settle(motor, Vector3.zero);
            // 段の上（x≈1.5）で打ち切る——歩きすぎると箱（x 0.7〜2.3）の先端から
            // 降りてしまい「登れたのに地面の高さ」で誤判定になる
            for (var i = 0; i < 60 && position.x < 1.5f; i++)
            {
                motor.PreTick(Step);
                position = motor.Move(position, new Vector3(0.05f, 0f, 0f));
            }

            Assert.That(position.x, Is.GreaterThan(1.0f), "段差の上まで前進できている");
            Assert.That(position.y, Is.EqualTo(0.18f).Within(0.09f), "段の上面に立っている");
            Assert.That(motor.IsGrounded, Is.True);
        }

        /// <summary>ジャンプは接地中だけ受理され、上昇→落下→着地まで一巡する。</summary>
        [Test]
        public void ジャンプして着地する()
        {
            CreateBox("Ground", new Vector3(0f, -0.5f, 0f), new Vector3(20f, 1f, 20f));
            var motor = CreateMotor(out _);
            var position = Settle(motor, Vector3.zero);

            Assert.That(motor.RequestJump(), Is.True, "接地中は跳べる");
            var peak = position.y;
            var airborneRejected = false;
            for (var i = 0; i < 150; i++)
            {
                motor.PreTick(Step);
                position = motor.Move(position, Vector3.zero);
                peak = Mathf.Max(peak, position.y);
                if (i == 20 && !motor.IsGrounded)
                {
                    airborneRejected = !motor.RequestJump(); // 滞空中の二段ジャンプは不可
                }
            }

            Assert.That(peak, Is.GreaterThan(0.5f), "頂点まで上がる（設計値 ≒ 1m）");
            Assert.That(airborneRejected, Is.True, "滞空中のジャンプは受理されない");
            Assert.That(motor.IsGrounded, Is.True, "着地して戻る");
            Assert.That(position.y, Is.EqualTo(0f).Within(0.08f));
        }

        /// <summary>実体が非アクティブ（2D切替中）なら素通しで動き、ジャンプ予約も残らない。</summary>
        [Test]
        public void 非アクティブ中は素通しになる()
        {
            var motor = CreateMotor(out var host);
            Assert.That(motor.RequestJump(), Is.True, "接地扱いのうちは予約できる");
            host.SetActive(false);
            motor.PreTick(Step);

            var moved = motor.Move(new Vector3(1f, 2f, 3f), new Vector3(0.5f, 0f, 0f));

            Assert.That(moved, Is.EqualTo(new Vector3(1.5f, 2f, 3f)));
            Assert.That(motor.VerticalVelocity, Is.LessThanOrEqualTo(0f),
                "素通し中に予約が消費されず捨てられる（復帰時に暴発しない）");
        }

        /// <summary>ホストを作ってモーターを装着する。</summary>
        private Sample_KinematicMotor CreateMotor(out GameObject host)
        {
            host = new GameObject("MotorHost");
            host.layer = 2; // Ignore Raycast（実機のプレイヤーと同じ＝足IKレイの自己ヒット回避）
            _spawned.Add(host);
            var motor = new Sample_KinematicMotor(host, height: 1.6f, originAtFeet: true);
            Physics.SyncTransforms();
            return motor;
        }

        /// <summary>初期位置から接地するまで落とす。</summary>
        private static Vector3 Settle(Sample_KinematicMotor motor, Vector3 start)
        {
            var position = start + Vector3.up * 0.2f;
            for (var i = 0; i < 60; i++)
            {
                motor.PreTick(Step);
                position = motor.Move(position, Vector3.zero);
            }
            return position;
        }

        /// <summary>静的な箱コライダーを置く（地形の部品）。</summary>
        private void CreateBox(string name, Vector3 center, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.position = center;
            var box = go.AddComponent<BoxCollider>();
            box.size = size;
            _spawned.Add(go);
            Physics.SyncTransforms();
        }
    }
}
