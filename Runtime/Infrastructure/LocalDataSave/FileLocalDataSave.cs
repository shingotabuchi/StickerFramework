using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using StickerFwk.Core;
using StickerFwk.Core.LocalDataSave;
using UnityEngine;

namespace StickerFwk.Infrastructure.LocalDataSave
{
    public sealed class FileLocalDataSave : ILocalDataSave
    {
        private readonly string _directoryPath;

        public FileLocalDataSave()
            : this(Path.Combine(Application.persistentDataPath, "Saves"))
        {
        }

        public FileLocalDataSave(string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                throw new ArgumentException("Directory path must not be empty.", nameof(directoryPath));
            }

            _directoryPath = directoryPath;
        }

        public UniTask<LocalDataSaveReadResult> ReadAsync(LocalDataSaveKey key, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var path = GetFilePath(key);
            if (!File.Exists(path))
            {
                return UniTask.FromResult(LocalDataSaveReadResult.Missing);
            }

            try
            {
                var bytes = File.ReadAllBytes(path);
                return UniTask.FromResult(new LocalDataSaveReadResult(true, bytes));
            }
            catch (Exception ex)
            {
                Log.Warning($"FileLocalDataSave: Failed to read '{path}': {ex.Message}");
                return UniTask.FromResult(LocalDataSaveReadResult.Missing);
            }
        }

        public async UniTask WriteAsync(LocalDataSaveKey key, ReadOnlyMemory<byte> data, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Directory.CreateDirectory(_directoryPath);
            var path = GetFilePath(key);
            var tempPath = path + ".tmp";

            await UniTask.SwitchToThreadPool();
            try
            {
                await using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await stream.WriteAsync(data, ct);
                    await stream.FlushAsync(ct);
                }

                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                File.Move(tempPath, path);
            }
            catch (Exception ex)
            {
                if (File.Exists(tempPath))
                {
                    try
                    {
                        File.Delete(tempPath);
                    }
                    catch
                    {
                        // Best-effort cleanup.
                    }
                }

                Log.Error($"FileLocalDataSave: Failed to write '{path}': {ex.Message}");
                throw;
            }
        }

        public UniTask DeleteAsync(LocalDataSaveKey key, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var path = GetFilePath(key);
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            return UniTask.CompletedTask;
        }

        private string GetFilePath(LocalDataSaveKey key)
        {
            return Path.Combine(_directoryPath, key.Value + ".json");
        }
    }
}
