using System.IO;
using NUnit.Framework;

namespace Seed.Persistence.Tests
{
    /// <summary>Seed.Persistence（封筒・原子的書き込み・破損防御）のテスト。</summary>
    public sealed class SaveStoreTests
    {
        /// <summary>テスト用の一時ディレクトリ。</summary>
        private string _directory;

        /// <summary>一時ディレクトリを用意する。</summary>
        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "SeedSaveStoreTests_" + Path.GetRandomFileName());
        }

        /// <summary>一時ディレクトリを片付ける。</summary>
        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }

        /// <summary>封筒: 包んで開けると同じペイロードと版数が返る。</summary>
        [Test]
        public void Envelope_Roundtrip()
        {
            var payload = new byte[] { 1, 2, 3, 250, 251, 252 };
            var wrapped = SaveEnvelope.Wrap(7, payload);

            Assert.IsTrue(SaveEnvelope.TryUnwrap(wrapped, out var version, out var unwrapped));
            Assert.AreEqual(7, version);
            CollectionAssert.AreEqual(payload, unwrapped);
        }

        /// <summary>封筒: 1バイトの改変・途中切れ・ゴミはすべて false。</summary>
        [Test]
        public void Envelope_RejectsCorruption()
        {
            var wrapped = SaveEnvelope.Wrap(1, new byte[] { 10, 20, 30 });

            var flipped = (byte[])wrapped.Clone();
            flipped[wrapped.Length / 2] ^= 0xFF; // 中身を1バイト改変
            Assert.IsFalse(SaveEnvelope.TryUnwrap(flipped, out _, out _), "改変はハッシュ不一致で拒否");

            var cut = new byte[wrapped.Length - 2];
            System.Array.Copy(wrapped, cut, cut.Length);
            Assert.IsFalse(SaveEnvelope.TryUnwrap(cut, out _, out _), "途中切れは拒否");

            Assert.IsFalse(SaveEnvelope.TryUnwrap(new byte[] { 9, 9, 9, 9, 9, 9, 9, 9 }, out _, out _), "ゴミは拒否");
            Assert.IsFalse(SaveEnvelope.TryUnwrap(null, out _, out _), "null は拒否");
        }

        /// <summary>ファイル保存: 往復・存在確認・削除が契約どおり動く。</summary>
        [Test]
        public void FileStore_SaveLoadDelete()
        {
            var store = new FileSaveStore(_directory);
            var data = new byte[] { 5, 4, 3, 2, 1 };

            Assert.IsFalse(store.Exists("slot1"));
            Assert.IsTrue(store.TrySave("slot1", data));
            Assert.IsTrue(store.Exists("slot1"));

            Assert.IsTrue(store.TryLoad("slot1", out var loaded));
            CollectionAssert.AreEqual(data, loaded);

            Assert.IsTrue(store.TrySave("slot1", new byte[] { 9 }), "上書き保存（原子的置換の経路）");
            Assert.IsTrue(store.TryLoad("slot1", out var overwritten));
            CollectionAssert.AreEqual(new byte[] { 9 }, overwritten);

            Assert.IsTrue(store.Delete("slot1"));
            Assert.IsFalse(store.Exists("slot1"));
            Assert.IsFalse(store.TryLoad("slot1", out _));
        }

        /// <summary>ファイル保存: ディスク上の破損は読み込みで検知され false になる。</summary>
        [Test]
        public void FileStore_RejectsCorruptedFile()
        {
            var store = new FileSaveStore(_directory);
            Assert.IsTrue(store.TrySave("slot1", new byte[] { 1, 2, 3 }));

            // ディスク上のファイルを直接破壊する（クラッシュ・改変の再現）
            var path = Path.Combine(_directory, "slot1.sav");
            var bytes = File.ReadAllBytes(path);
            bytes[bytes.Length - 1] ^= 0xFF;
            File.WriteAllBytes(path, bytes);

            Assert.IsFalse(store.TryLoad("slot1", out _), "破損は例外ではなく false");
        }

        /// <summary>ファイル保存: パス注入になりうる key は拒否される。</summary>
        [Test]
        public void FileStore_RejectsInvalidKeys()
        {
            var store = new FileSaveStore(_directory);
            Assert.IsFalse(store.TrySave("../escape", new byte[] { 1 }));
            Assert.IsFalse(store.TrySave("a/b", new byte[] { 1 }));
            Assert.IsFalse(store.TrySave("", new byte[] { 1 }));
            Assert.IsTrue(store.TrySave("slot-1_backup.v2", new byte[] { 1 }), "英数字と -_. は許可");
        }

        /// <summary>メモリ保存: 契約の挙動が同じで、コピーが保持される。</summary>
        [Test]
        public void MemoryStore_BehavesLikeContract()
        {
            var store = new MemorySaveStore();
            var data = new byte[] { 1, 2 };
            Assert.IsTrue(store.TrySave("slot1", data));

            data[0] = 99; // 呼び出し側の後書きは保存内容に影響しない
            Assert.IsTrue(store.TryLoad("slot1", out var loaded));
            Assert.AreEqual(1, loaded[0]);

            Assert.IsTrue(store.Delete("slot1"));
            Assert.IsFalse(store.TryLoad("slot1", out _));
        }
    }
}
