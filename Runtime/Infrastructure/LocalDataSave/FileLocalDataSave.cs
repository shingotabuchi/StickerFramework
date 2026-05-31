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
        private const string SaveFolderName = "Saves";
        private const string SaveExtension = ".json";
        private const string TempExtension = ".tmp";
        private const string Tag = "LocalDataSave";

        private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

        private readonly string _saveDirectory;

        public FileLocalDataSave()
        {
            EnsureConstructedOnMainThread();
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
                var tempPath = CreateTempPath(path);

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

        private static string CreateTempPath(string path)
        {
            return path + "." + Guid.NewGuid().ToString("N") + TempExtension;
        }

        private static string CreateBackupPath(string path)
        {
            return path + "." + Guid.NewGuid().ToString("N") + ".bak";
        }

        private static void EnsureConstructedOnMainThread()
        {
            if (Thread.CurrentThread.IsBackground || Thread.CurrentThread.IsThreadPoolThread)
            {
                throw new InvalidOperationException(
                    "FileLocalDataSave must be constructed on the Unity main thread because it reads Application.persistentDataPath.");
            }
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
            if (!File.Exists(path))
            {
                File.Move(tempPath, path);
                return;
            }

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

            ReplaceFileWithBackup(tempPath, path);
        }

        private static void ReplaceFileWithBackup(string tempPath, string path)
        {
            var backupPath = CreateBackupPath(path);
            var replaced = false;

            File.Move(path, backupPath);
            try
            {
                File.Move(tempPath, path);
                replaced = true;
            }
            catch
            {
                TryRestoreBackup(backupPath, path);
                throw;
            }
            finally
            {
                if (replaced && File.Exists(backupPath))
                {
                    try
                    {
                        File.Delete(backupPath);
                    }
                    catch (IOException ex)
                    {
                        LogBackupDeletionFailure(backupPath, ex);
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        LogBackupDeletionFailure(backupPath, ex);
                    }
                }
            }
        }

        private static void TryRestoreBackup(string backupPath, string path)
        {
            if (File.Exists(path) || !File.Exists(backupPath))
            {
                return;
            }

            try
            {
                File.Move(backupPath, path);
            }
            catch (Exception ex)
            {
                Log.Warning(
                    Tag,
                    $"Failed to restore local data backup '{backupPath}' to '{path}'. Save data may be recoverable at the backup path. {ex}");
            }
        }

        private static void LogBackupDeletionFailure(string backupPath, Exception ex)
        {
            Log.Warning(Tag, $"Failed to delete local data backup '{backupPath}'. The primary save file is already in place. {ex}");
        }
    }
}
