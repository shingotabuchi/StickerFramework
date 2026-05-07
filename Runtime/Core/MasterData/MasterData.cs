using System;
using UnityEngine;

namespace StickerFwk.Core.MasterData
{
    public class MasterData<T> : IMasterData where T : MasterData<T>
    {
        [SerializeField] private string _id;
        public string Id => _id;
        public Type Type => typeof(T);
    }
}