using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace MafiaDiscordBot.Models
{
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public class ThreadSafeList<T> : IList<T>
    {
        private readonly List<T> _internalList;
        private readonly object _lock = new object();

        public ThreadSafeList()
        {
            _internalList = new List<T>();
        }
        public ThreadSafeList(int capacity)
        {
            _internalList = new List<T>(capacity);
        }
        public ThreadSafeList(IEnumerable<T> collection)
        {
            _internalList = new List<T>(collection);
        }

        public IEnumerator<T> GetEnumerator() => Clone().GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => Clone().GetEnumerator();

        public void Add(T item)
        {
            lock (_lock)
                _internalList.Add(item);
        }

        public void Clear()
        {
            lock (_lock)
                _internalList.Clear();
        }

        public bool Contains(T item)
        {
            lock (_lock)
                return _internalList.Contains(item);
        }
        public void CopyTo(T[] array, int arrayIndex)
        {
            lock (_lock)
                _internalList.CopyTo(array, arrayIndex);
        }

        public bool Remove(T item)
        {
            lock (_lock)
                return _internalList.Remove(item);
        }

        public int Count {
            get
            {
                lock (_lock) 
                    return _internalList.Count;
            }
        }

        public bool IsReadOnly => false;
        public int IndexOf(T item)
        {
            lock (_lock) 
                return _internalList.IndexOf(item);
        }

        public void Insert(int index, T item)
        {
            lock (_lock)
                _internalList.Insert(index, item);
        }

        public void RemoveAt(int index)
        {
            lock (_lock)
                _internalList.RemoveAt(index);
        }

        public T this[int index]
        {
            get
            {
                lock (_lock)
                    return _internalList[index];
            }
            set
            {
                lock (_lock)
                    _internalList[index] = value;
            }
        }

        private List<T> Clone()
        {
            var newList = new List<T>();

            lock (_lock)
                _internalList.ForEach(x => newList.Add(x));

            return newList;
        }
    }
}