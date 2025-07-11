using System;
using System.Collections;
using System.Collections.Generic;



namespace ProxyStudio.Behaviors
{
    /// <summary>
    /// Helper class to wrap IList<T> as IList for compatibility with older collection interfaces
    /// </summary>
    public class ListWrapper<T> : IList
    {
        private readonly System.Collections.Generic.IList<T> _list;

        public ListWrapper(IList<T> list)
        {
            _list = list ?? throw new ArgumentNullException(nameof(list));
        }

        public int Add(object? value)
        {
            if (value is T item)
            {
                _list.Add(item);
                return _list.Count - 1;
            }
            return -1;
        }

        public void Clear() => _list.Clear();

        public bool Contains(object? value) => value is T item && _list.Contains(item);

        public int IndexOf(object? value) => value is T item ? _list.IndexOf(item) : -1;

        public void Insert(int index, object? value)
        {
            if (value is T item)
                _list.Insert(index, item);
        }

        public void Remove(object? value)
        {
            if (value is T item)
                _list.Remove(item);
        }

        public void RemoveAt(int index) => _list.RemoveAt(index);

        public bool IsFixedSize => false;
        public bool IsReadOnly => _list.IsReadOnly;

        public object? this[int index]
        {
            get => _list[index];
            set
            {
                if (value is T item)
                    _list[index] = item;
            }
        }

        public void CopyTo(Array array, int index) => ((ICollection)_list).CopyTo(array, index);

        public int Count => _list.Count;

        public bool IsSynchronized => false;

        public object SyncRoot => this;

        public IEnumerator GetEnumerator() => _list.GetEnumerator();
    }
}

