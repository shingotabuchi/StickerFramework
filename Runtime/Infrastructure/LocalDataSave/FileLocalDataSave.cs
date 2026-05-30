using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using StickerFwk.Core.LocalDataSave;
using UnityEngine;

namespace StickerFwk.Infrastructure.LocalDataSave
{
    public sealed class FileLocalDataSave : ILocalDataSave
    {
        private const string SaveFolderName = "Saves";
        private const string SaveExtension = ".json";
        private const string TempExtension = ".tmp";

        private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

        private readonly string _saveDirectory;

        public FileLocalDataSave()
        {
            _saveDirectory = Path.Combine(Application.persistentDataPath, SaveFolderName);
        }

        public UniTask<byte[]> ReadAsync(LocalDataSaveKey key, CancellationToken ct)
        {
            ValidateKey(key);
            return UniTask.RunOnThreadPool(() =>
            {
                ct.ThrowIfCancellationRequested();

                var path = GetPath(key);
                if (!File.Exists(path))
                {
                    return null;
                }

                return File.ReadAllBytes(path);
            }, cancellationToken: ct);
        }

        public UniTask WriteAsync(LocalDataSaveKey key, ReadOnlyMemory<byte> data, CancellationToken ct)
        {
            ValidateKey(key);
            return UniTask.RunOnThreadPool(() =>
            {
                ct.ThrowIfCancellationRequested();

                Directory.CreateDirectory(_saveDirectory);
                var path = GetPath(key);
                var tempPath = path + TempExtension;

                try
                {
                    File.WriteAllBytes(tempPath, data.ToArray());
                    ReplaceFile(tempPath, path);
                }
                finally
                {
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                }
            }, cancellationToken: ct);
        }

        public UniTask DeleteAsync(LocalDataSaveKey key, CancellationToken ct)
        {
            ValidateKey(key);
            return UniTask.RunOnThreadPool(() =>
            {
                ct.ThrowIfCancellationRequested();

                var path = GetPath(key);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }, cancellationToken: ct);
        }

        private string GetPath(LocalDataSaveKey key)
        {
            return Path.Combine(_saveDirectory, key.Value + SaveExtension);
        }

        private static void ValidateKey(LocalDataSaveKey key)
        {
            if (!key.IsValid)
            {
                throw new ArgumentException("Local data save key must be valid.", nameof(key));
            }

            if (key.Value.IndexOfAny(InvalidFileNameChars) >= 0)
            {
                throw new ArgumentException($"Local data save key '{key.Value}' contains invalid file name characters.", nameof(key));
            }
        }

        private static void ReplaceFile(string tempPath, string path)
        {
            if (File.Exists(path))
            {
                try
                {
                    File.Replace(tempPath, path, null);
                    return;
                }
                catch (IOException)
                {
                }
                catch (PlatformNotSupportedException)
                {
                }
            }

            if (File.Exists(path))
            {
                File.Delete(path);
            }

            File.Move(tempPath, path);
        }
    }
}
