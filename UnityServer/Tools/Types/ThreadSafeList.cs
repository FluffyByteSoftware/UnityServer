using System;
using System.Collections;
using System.Collections.Generic;

namespace UnityServer.Tools.Types
{
    public class ThreadSafeList<T> : IEnumerable<T>
    {
        private readonly List<T> _list = [];
        private readonly System.Threading.Lock _lock = new();

        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _list.Count;
                }
            }
        }

        public bool TryGet(int index, out T? value)
        {
            lock(_lock)
            {
                if(index >= 0 && index < _list.Count)
                {
                    value = _list[index];
                    return true;
                }

                value = default;
                return false;
            }
        }

        public void Add(T item)
        {
            lock (_lock)
            {
                _list.Add(item);
            }
        }

        public void AddRange(IEnumerable<T> items)
        {
            lock(_lock)
            {
                _list.AddRange(items);
            }
        }

        public bool Remove(T item)
        {
            lock (_lock)
            {
                return _list.Remove(item);
            }
        }

        public int RemoveAll(Predicate<T> match)
        {
            lock(_lock)
            {
                return _list.RemoveAll(match);
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _list.Clear();
            }
        }

        public bool Contains(T item)
        {
            lock (_lock)
            {
                return _list.Contains(item);
            }
        }

        public T? Find(Predicate<T> match)
        {
            lock (_lock)
            {
                return _list.Find(match);
            }
        }

        public List<T> ToList()
        {
            lock (_lock)
            {
                return new List<T>(_list);
            }
        }

        public T this[int index]
        {
            get
            {
                lock (_lock)
                {
                    return _list[index];
                }
            }
            set
            {
                lock (_lock)
                {
                    _list[index] = value;
                }
            }
        }

        // Implements IEnumerable<T> for foreach support
        public IEnumerator<T> GetEnumerator()
        {
            lock (_lock)
            {
                foreach(T item in _list)
                {
                    yield return item;
                }
                
            }
        }


        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
