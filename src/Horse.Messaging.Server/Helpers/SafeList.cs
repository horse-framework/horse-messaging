using System;
using System.Collections.Generic;

namespace Horse.Messaging.Server.Helpers
{
    /// <summary>
    /// Thread safe list
    /// </summary>
    public class SafeList<T>
    {
        /// <summary>
        /// Real slim data
        /// </summary>
        private readonly List<T> _list;

        /// <summary>
        /// Creates new safe list
        /// </summary>
        public SafeList(int capacity)
        {
            _list = new List<T>(capacity);
        }

        /// <summary>
        /// Returns the list itself.
        /// This method does not return a thread-safe list.
        /// </summary>
        public IEnumerable<T> GetUnsafeList() => _list;

        /// <summary>
        /// Item count in list
        /// </summary>
        public int Count => _list.Count;

        /// <summary>
        /// Clones all items to a safe list and returns the safe list
        /// </summary>
        /// <returns></returns>
        public List<T> GetAsClone()
        {
            List<T> list;

            lock (_list)
                list = new List<T>(_list);

            return list;
        }

        /// <summary>
        /// Clears list, removes all items
        /// </summary>
        public void Clear()
        {
            lock (_list)
                _list.Clear();
        }

        /// <summary>
        /// Adds and item to the list
        /// </summary>
        public void Add(T item)
        {
            lock (_list)
                _list.Add(item);
        }

        /// <summary>
        /// Removes an object from the list
        /// </summary>
        public void Remove(T item)
        {
            if (item == null)
                return;

            lock (_list)
                _list.Remove(item);
        }

        /// <summary>
        /// Removes an object from the list
        /// </summary>
        public void Remove(int index)
        {
            lock (_list)
            {
                if (_list.Count <= index)
                    return;

                _list.RemoveAt(index);
            }
        }

        /// <summary>
        /// Removes objects from the list
        /// </summary>
        public void Remove(Predicate<T> predicate, bool removeOnlyFirst)
        {
            if (removeOnlyFirst)
            {
                lock (_list)
                    for (int i = 0; i < _list.Count; i++)
                    {
                        T item = _list[i];
                        if (predicate(item))
                        {
                            _list.RemoveAt(i);
                            return;
                        }
                    }
            }

            lock (_list)
                _list.RemoveAll(predicate);
        }

        /// <summary>
        /// Removes the object, at specified index, from the last
        /// </summary>
        public void RemoveAt(int index)
        {
            lock (_list)
            {
                if (_list.Count > index)
                    _list.RemoveAt(index);
            }
        }

        /// <summary>
        /// Removes objects from the list
        /// </summary>
        public T FindAndRemove(Predicate<T> predicate)
        {
            lock (_list)
                for (int i = 0; i < _list.Count; i++)
                {
                    T item = _list[i];
                    if (predicate(item))
                    {
                        _list.RemoveAt(i);
                        return item;
                    }
                }

            return default;
        }

        /// <summary>
        /// Finds the element from the array
        /// </summary>
        public T Find(Predicate<T> predicate)
        {
            lock (_list)
                foreach (T item in _list)
                {
                    if (predicate(item))
                        return item;
                }

            return default;
        }

        /// <summary>
        /// Finds all elements within a filter
        /// </summary>
        public List<T> FindAll(Predicate<T> predicate)
        {
            List<T> items = new List<T>();
            lock (_list)
                foreach (T item in _list)
                {
                    if (predicate(item))
                        items.Add(item);
                }

            return items;
        }
    }
}