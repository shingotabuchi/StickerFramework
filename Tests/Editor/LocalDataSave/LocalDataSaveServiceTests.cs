using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using StickerFwk.Core.LocalDataSave;
using StickerFwk.Infrastructure.LocalDataSave;

namespace StickerFwk.Tests.Editor.LocalDataSave
{
    public sealed class LocalDataSaveServiceTests
    {
        private readonly ILocalDataSave _fileSave = new FileLocalDataSave();
        private readonly ILocalDataSaveSerializer _serializer = new JsonLocalDataSaveSerializer();

        [Test]
        public void LocalDataSaveKeyRejectsEmptyValue()
        {
            Assert.Throws<ArgumentException>(() => new LocalDataSaveKey(string.Empty));
        }

        [Test]
        public async Task MissingKeyReturnsFreshDto()
        {
            var key = CreateKey();
            var service = CreateService();

            await service.DeleteAsync(key, CancellationToken.None);
            var loaded = await service.LoadAsync<TestSaveData>(key, CancellationToken.None).AsTask();

            Assert.That(loaded.Name, Is.Null);
            Assert.That(loaded.Count, Is.EqualTo(0));
        }

        [Test]
        public async Task SaveThenLoadRoundTripsDto()
        {
            var key = CreateKey();
            var service = CreateService();

            try
            {
                await service.SaveAsync(key, new TestSaveData
                {
                    Name = "stage-1",
                    Count = 3
                }, CancellationToken.None);

                var loaded = await service.LoadAsync<TestSaveData>(key, CancellationToken.None).AsTask();

                Assert.That(loaded.Name, Is.EqualTo("stage-1"));
                Assert.That(loaded.Count, Is.EqualTo(3));
            }
            finally
            {
                await service.DeleteAsync(key, CancellationToken.None);
            }
        }

        [Test]
        public async Task OverwriteReplacesExistingData()
        {
            var key = CreateKey();
            var service = CreateService();

            try
            {
                await service.SaveAsync(key, new TestSaveData
                {
                    Name = "old",
                    Count = 1
                }, CancellationToken.None);
                await service.SaveAsync(key, new TestSaveData
                {
                    Name = "new",
                    Count = 2
                }, CancellationToken.None);

                var loaded = await service.LoadAsync<TestSaveData>(key, CancellationToken.None).AsTask();

                Assert.That(loaded.Name, Is.EqualTo("new"));
                Assert.That(loaded.Count, Is.EqualTo(2));
            }
            finally
            {
                await service.DeleteAsync(key, CancellationToken.None);
            }
        }

        [Test]
        public async Task DeleteRemovesSavedData()
        {
            var key = CreateKey();
            var service = CreateService();

            await service.SaveAsync(key, new TestSaveData
            {
                Name = "saved",
                Count = 7
            }, CancellationToken.None);

            await service.DeleteAsync(key, CancellationToken.None);
            var loaded = await service.LoadAsync<TestSaveData>(key, CancellationToken.None).AsTask();

            Assert.That(loaded.Name, Is.Null);
            Assert.That(loaded.Count, Is.EqualTo(0));
        }

        [Test]
        public async Task CorruptJsonReturnsFreshDto()
        {
            var key = CreateKey();
            var service = CreateService();
            var corruptBytes = Encoding.UTF8.GetBytes("{not-json");

            try
            {
                await _fileSave.WriteAsync(key, corruptBytes, CancellationToken.None);

                var loaded = await service.LoadAsync<TestSaveData>(key, CancellationToken.None).AsTask();

                Assert.That(loaded.Name, Is.Null);
                Assert.That(loaded.Count, Is.EqualTo(0));
            }
            finally
            {
                await service.DeleteAsync(key, CancellationToken.None);
            }
        }

        private ILocalDataSaveService CreateService()
        {
            return new LocalDataSaveService(_fileSave, _serializer);
        }

        private static LocalDataSaveKey CreateKey()
        {
            return new LocalDataSaveKey($"test.{Guid.NewGuid():N}");
        }

        public sealed class TestSaveData
        {
            public string Name { get; set; }

            public int Count { get; set; }
        }
    }
}
