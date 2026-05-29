using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using StickerFwk.Core.LocalDataSave;
using StickerFwk.Infrastructure.LocalDataSave;

namespace StickerFwk.Tests.Runtime.Infrastructure.LocalDataSave
{
    public sealed class LocalDataSaveServiceTests
    {
        private string _tempDirectory;

        [SetUp]
        public void SetUp()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "StickerLocalDataSaveTests", Path.GetRandomFileName());
            Directory.CreateDirectory(_tempDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }

        [Test]
        public async UniTask RoundTripPersistsSaveData()
        {
            var service = CreateService();
            var migrator = new TestSaveMigrator();
            var key = new LocalDataSaveKey("test.roundtrip");
            var payload = new TestSaveData { SaveVersion = 1, Label = "hello" };

            await service.SaveAsync(key, payload, CancellationToken.None);
            var loaded = await service.LoadAsync(key, migrator, CancellationToken.None);

            Assert.That(loaded.Label, Is.EqualTo("hello"));
        }

        [Test]
        public async UniTask CorruptFileReturnsDefault()
        {
            var fileSave = new FileLocalDataSave(_tempDirectory);
            var key = new LocalDataSaveKey("test.corrupt");
            var corruptPath = Path.Combine(_tempDirectory, key.Value + ".json");
            await File.WriteAllTextAsync(corruptPath, "{ this is not json");

            var service = CreateService(fileSave);
            var loaded = await service.LoadAsync(key, new TestSaveMigrator(), CancellationToken.None);

            Assert.That(loaded.Label, Is.EqualTo("default"));
        }

        [Test]
        public async UniTask TransactionRollbackDoesNotWriteFiles()
        {
            var fileSave = new FileLocalDataSave(_tempDirectory);
            var service = CreateService(fileSave);
            var key = new LocalDataSaveKey("test.rollback");

            try
            {
                await service.ExecuteInTransactionAsync(async (transaction, ct) =>
                {
                    await transaction.SaveAsync(key, new TestSaveData { Label = "pending" }, ct);
                    throw new System.InvalidOperationException("force rollback");
                }, CancellationToken.None);
            }
            catch (System.InvalidOperationException)
            {
                // Expected.
            }

            var path = Path.Combine(_tempDirectory, key.Value + ".json");
            Assert.That(File.Exists(path), Is.False);
        }

        [Test]
        public async UniTask TransactionCommitWritesAllKeys()
        {
            var fileSave = new FileLocalDataSave(_tempDirectory);
            var service = CreateService(fileSave);
            var progressKey = new LocalDataSaveKey("test.progress");
            var settingsKey = new LocalDataSaveKey("test.settings");

            await service.ExecuteInTransactionAsync(async (transaction, ct) =>
            {
                await transaction.SaveAsync(progressKey, new TestSaveData { Label = "progress" }, ct);
                await transaction.SaveAsync(settingsKey, new TestSaveData { Label = "settings" }, ct);
            }, CancellationToken.None);

            var progress = await service.LoadAsync(progressKey, new TestSaveMigrator(), CancellationToken.None);
            var settings = await service.LoadAsync(settingsKey, new TestSaveMigrator(), CancellationToken.None);
            Assert.That(progress.Label, Is.EqualTo("progress"));
            Assert.That(settings.Label, Is.EqualTo("settings"));
        }

        private LocalDataSaveService CreateService(FileLocalDataSave fileSave = null)
        {
            return new LocalDataSaveService(
                fileSave ?? new FileLocalDataSave(_tempDirectory),
                new JsonLocalDataSaveSerializer(),
                new PassThroughLocalDataSaveProtector());
        }

        private sealed class TestSaveData : ISaveVersioned
        {
            public int SaveVersion { get; set; } = 1;

            public string Label { get; set; } = "default";
        }

        private sealed class TestSaveMigrator : ILocalDataSaveMigrator<TestSaveData>
        {
            public int CurrentVersion => 1;

            public TestSaveData CreateDefault()
            {
                return new TestSaveData();
            }

            public TestSaveData Migrate(TestSaveData data, int loadedVersion)
            {
                data.SaveVersion = CurrentVersion;
                return data;
            }
        }
    }
}
