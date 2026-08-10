using System.Collections.Generic;
using NUnit.Framework;
using Seed.Motion;
using UnityEngine;

namespace Seed.App.Tests
{
    /// <summary>
    /// プレイヤーモデル束（標準＝StarterAssets / 揺れものデモ＝UnityChan）のテスト。
    /// 2つは完全に別のサンプルであり、共通契約（Sample_IPlayerModel）越しに
    /// 期待どおりの駆動方式・構成で組み上がることを確認する。
    /// </summary>
    public sealed class PlayerModelTests
    {
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
        }

        /// <summary>揺れものデモ（UnityChan）は Playables 直駆動＋揺れもの付きで組まれる。</summary>
        [Test]
        public void 揺れものデモはPlayables直駆動で揺れもの付き()
        {
            Sample_IPlayerModel model = Sample_PlayerModel.TryCreate(null);
            Assert.That(model, Is.Not.Null, "セットアップ済みプレハブから組める");
            Track(model);

            Assert.That(model.IsControllerDriven, Is.False);
            Assert.That(model.Avatar, Is.InstanceOf<RiggedAvatar>());
            Assert.That(model.Driver, Is.Not.Null, "Playables の再生器を持つ");
            Assert.That(model.ControllerAvatar, Is.Null);
            Assert.That(model.Springs.Count, Is.GreaterThan(0), "揺れものチェーンを持つ");
        }

        /// <summary>標準（StarterAssets）は Controller 駆動＋揺れもの無しで組まれる。</summary>
        [Test]
        public void 標準はController駆動で揺れもの無し()
        {
            Sample_IPlayerModel model = Sample_StarterPlayerModel.TryCreate(null);
            Assert.That(model, Is.Not.Null, "セットアップ済みプレハブから組める");
            Track(model);

            Assert.That(model.IsControllerDriven, Is.True);
            Assert.That(model.Avatar, Is.InstanceOf<AnimatorAvatar>());
            Assert.That(model.ControllerAvatar.Animator.runtimeAnimatorController, Is.Not.Null,
                "遷移なし Controller が割り当てられている");
            Assert.That(model.Driver, Is.Null, "Playables の再生器は持たない");
            Assert.That(model.Springs.Count, Is.EqualTo(0), "揺れものは無し（仕様）");
        }

        /// <summary>どちらのモデルも共通契約の必須ボーンを備える（リグ装着の前提）。</summary>
        [Test]
        public void 両モデルとも必須ボーンを備える()
        {
            Sample_IPlayerModel unityChan = Sample_PlayerModel.TryCreate(null);
            Sample_IPlayerModel starter = Sample_StarterPlayerModel.TryCreate(null);
            Assert.That(unityChan, Is.Not.Null);
            Assert.That(starter, Is.Not.Null);
            Track(unityChan);
            Track(starter);

            foreach (var model in new[] { unityChan, starter })
            {
                Assert.That(model.Head, Is.Not.Null);
                Assert.That(model.Hips, Is.Not.Null);
                Assert.That(model.Hand, Is.Not.Null);
                Assert.That(model.Height, Is.GreaterThan(1f));
            }
        }

        /// <summary>モデルのルートを後始末リストへ登録する。</summary>
        private void Track(Sample_IPlayerModel model)
        {
            _spawned.Add(((Component)model.Avatar).gameObject);
        }
    }
}
