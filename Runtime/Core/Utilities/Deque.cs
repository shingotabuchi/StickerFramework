using System;
using System.Collections;
using System.Collections.Generic;

namespace StickerFwk.Core
{
    public class Deque<T> : IEnumerable<T>
    {
        public event Action<T> OnItemRemoved;
        private readonly LinkedList<T> _list = new();

        public int Count => _list.Count;

        public void ReAddToFront(T item)
        {
            _list.Remove(item);
            _list.AddFirst(item);
        }

        public void AddToFront(T item)
        {
            _list.AddFirst(item);
        }

        public void AddToBack(T item)
        {
            _list.AddLast(item);
        }

        public void Remove(T item)
        {
            if (_list.Remove(item))
            {
                OnItemRemoved?.Invoke(item);
            }
        }

        public T RemoveFromFront()
        {
            if (_list.Count == 0)
            {
                throw new InvalidOperationException("Deque is empty.");
            }
            var value = _list.First.Value;
            _list.RemoveFirst();
            OnItemRemoved?.Invoke(value);
            return value;
        }

        public T RemoveFromBack()
        {
            if (_list.Count == 0)
            {
                throw new InvalidOperationException("Deque is empty.");
            }
            var value = _list.Last.Value;
            _list.RemoveLast();
            OnItemRemoved?.Invoke(value);
            return value;
        }

        public T PeekFront()
        {
            if (_list.Count == 0)
            {
                return default(T);
            }
            return _list.First.Value;
        }

        public T PeekBack()
        {
            if (_list.Count == 0)
            {
                return default(T);
            }
            return _list.Last.Value;
        }

        public bool Contains(T item)
        {
            return _list.Contains(item);
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _list.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Clear()
        {
            foreach (var item in _list)
            {
                OnItemRemoved?.Invoke(item);
            }

            _list.Clear();
        }
    }
}
