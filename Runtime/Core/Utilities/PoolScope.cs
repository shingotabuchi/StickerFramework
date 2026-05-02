using System.Collections.Generic;
using UnityEngine.Pool;

namespace StickerFwk.Core
{
    public static class PoolScope
    {
        public static PooledObject<List<T>> List<T>(out List<T> list)
        {
            return ListPool<T>.Get(out list);
        }

        public static PooledObject<HashSet<T>> HashSet<T>(out HashSet<T> set)
        {
            return CollectionPool<HashSet<T>, T>.Get(out set);
        }
    }
}
