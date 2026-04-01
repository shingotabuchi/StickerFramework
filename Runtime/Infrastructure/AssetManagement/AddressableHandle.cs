using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace StickerFwk.Infrastructure.AssetManagement
{
    public class AddressableHandle<T> : IAddressableHandle
    {
        private readonly AsyncOperationHandle<T> _handle;

        public AddressableHandle(AsyncOperationHandle<T> handle)
        {
            _handle = handle;
        }

        public Object Object => _handle.Result as Object;
        public IReadOnlyList<Object> Objects => _handle.Result as IReadOnlyList<Object>;
        public bool Succeeded => _handle.Status == AsyncOperationStatus.Succeeded;

        public void Release()
        {
            if (_handle.IsValid()) Addressables.Release(_handle);
        }
    }
}