using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using StickerFwk.Core;
using StickerFwk.Core.LocalDataSave;

namespace StickerFwk.Infrastructure.LocalDataSave
{
    public sealed class LocalDataSaveService : ILocalDataSaveService
    {
        private readonly ILocalDataSave _localDataSave;
        private readonly ILocalDataSaveSerializer _serializer;
        private readonly ILocalDataSaveProtector _protector;
        private bool _transactionOpen;

        public LocalDataSaveService(
            ILocalDataSave localDataSave,
            ILocalDataSaveSerializer serializer,
            ILocalDataSaveProtector protector)
        {
            _localDataSave = localDataSave ?? throw new ArgumentNullException(nameof(localDataSave));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _protector = protector ?? throw new ArgumentNullException(nameof(protector));
        }

        public async UniTask<T> LoadAsync<T>(
            LocalDataSaveKey key,
            ILocalDataSaveMigrator<T> migrator,
            CancellationToken ct) where T : class, new()
        {
            if (migrator == null)
            {
                throw new ArgumentNullException(nameof(migrator));
            }

            var readResult = await _localDataSave.ReadAsync(key, ct);
            if (!readResult.Found)
            {
                return migrator.CreateDefault();
            }

            try
            {
                var plainBytes = _protector.Unprotect(readResult.Bytes);
                var data = _serializer.Deserialize<T>(plainBytes);
                if (data == null)
                {
                    Log.Warning($"LocalDataSaveService: Empty payload for key '{key.Value}'. Using defaults.");
                    return migrator.CreateDefault();
                }

                var version = GetSaveVersion(data);
                if (version > migrator.CurrentVersion)
                {
                    Log.Warning(
                        $"LocalDataSaveService: Key '{key.Value}' has future version {version}; using defaults.");
                    return migrator.CreateDefault();
                }

                while (version < migrator.CurrentVersion)
                {
                    data = migrator.Migrate(data, version);
                    version = GetSaveVersion(data);
                }

                return data;
            }
            catch (Exception ex)
            {
                Log.Warning($"LocalDataSaveService: Failed to load key '{key.Value}': {ex.Message}. Using defaults.");
                return migrator.CreateDefault();
            }
        }

        public async UniTask SaveAsync<T>(LocalDataSaveKey key, T data, CancellationToken ct) where T : class
        {
            EnsureNoTransactionConflict();
            var protectedBytes = SerializeAndProtect(data);
            await _localDataSave.WriteAsync(key, protectedBytes, ct);
        }

        public async UniTask ExecuteInTransactionAsync(
            Func<ILocalDataSaveTransaction, CancellationToken, UniTask> action,
            CancellationToken ct)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            await using var transaction = await BeginTransactionInternalAsync(ct);
            try
            {
                await action(transaction, ct);
                await transaction.CommitAsync(ct);
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        }

        public async UniTask<ILocalDataSaveTransaction> BeginTransactionAsync(CancellationToken ct)
        {
            return await BeginTransactionInternalAsync(ct);
        }

        private async UniTask<LocalDataSaveTransaction> BeginTransactionInternalAsync(CancellationToken ct)
        {
            if (_transactionOpen)
            {
                throw new InvalidOperationException("A local data save transaction is already open.");
            }

            _transactionOpen = true;
            var transaction = new LocalDataSaveTransaction(this);
            await UniTask.Yield(ct);
            return transaction;
        }

        internal void ReleaseTransaction()
        {
            _transactionOpen = false;
        }

        internal byte[] SerializeAndProtect<T>(T data) where T : class
        {
            var plainBytes = _serializer.Serialize(data);
            return _protector.Protect(plainBytes);
        }

        internal async UniTask CommitPendingAsync(
            IReadOnlyList<KeyValuePair<LocalDataSaveKey, byte[]>> pending,
            CancellationToken ct)
        {
            EnsureNoTransactionConflict();
            for (var i = 0; i < pending.Count; i++)
            {
                var entry = pending[i];
                await _localDataSave.WriteAsync(entry.Key, entry.Value, ct);
            }
        }

        private void EnsureNoTransactionConflict()
        {
            if (_transactionOpen)
            {
                throw new InvalidOperationException(
                    "Cannot perform immediate save while a local data save transaction is open.");
            }
        }

        private static int GetSaveVersion<T>(T data) where T : class
        {
            if (data is ISaveVersioned versioned)
            {
                return versioned.SaveVersion;
            }

            return 1;
        }

        private sealed class LocalDataSaveTransaction : ILocalDataSaveTransaction, IAsyncDisposable
        {
            private readonly LocalDataSaveService _owner;
            private readonly List<KeyValuePair<LocalDataSaveKey, byte[]>> _pending =
                new List<KeyValuePair<LocalDataSaveKey, byte[]>>();
            private bool _committed;
            private bool _rolledBack;

            public LocalDataSaveTransaction(LocalDataSaveService owner)
            {
                _owner = owner;
            }

            public UniTask SaveAsync<T>(LocalDataSaveKey key, T data, CancellationToken ct) where T : class
            {
                ct.ThrowIfCancellationRequested();
                EnsureActive();
                var protectedBytes = _owner.SerializeAndProtect(data);
                for (var i = 0; i < _pending.Count; i++)
                {
                    if (_pending[i].Key == key)
                    {
                        _pending[i] = new KeyValuePair<LocalDataSaveKey, byte[]>(key, protectedBytes);
                        return UniTask.CompletedTask;
                    }
                }

                _pending.Add(new KeyValuePair<LocalDataSaveKey, byte[]>(key, protectedBytes));
                return UniTask.CompletedTask;
            }

            public async UniTask CommitAsync(CancellationToken ct)
            {
                EnsureActive();
                for (var i = 0; i < _pending.Count; i++)
                {
                    var entry = _pending[i];
                    await _owner._localDataSave.WriteAsync(entry.Key, entry.Value, ct);
                }

                _committed = true;
                _owner.ReleaseTransaction();
            }

            public UniTask RollbackAsync(CancellationToken ct)
            {
                ct.ThrowIfCancellationRequested();
                if (!_committed && !_rolledBack)
                {
                    _pending.Clear();
                    _rolledBack = true;
                    _owner.ReleaseTransaction();
                }

                return UniTask.CompletedTask;
            }

            public async ValueTask DisposeAsync()
            {
                if (!_committed && !_rolledBack)
                {
                    await RollbackAsync(CancellationToken.None);
                }
            }

            private void EnsureActive()
            {
                if (_committed || _rolledBack)
                {
                    throw new InvalidOperationException("The local data save transaction is no longer active.");
                }
            }
        }
    }
}
