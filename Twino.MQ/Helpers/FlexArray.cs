using System;
using System.Collections.Generic;

namespace Twino.MQ.Helpers
{
    /// <summary>
    /// Flex array is a flexible array that consists of multiple arrays.
    /// Flex array  provides not creating huge arrays with huge allocation
    /// And supports multi-threaded enumeration without locking arrays.
    /// </summary>
    public class FlexArray<T>
        where T : class
    {
        /// <summary>
        /// Data
        /// </summary>
        private T[][] _arrays;

        /// <summary>
        /// Size of each array in flex array
        /// </summary>
        private int ArraySize { get; }

        /// <summary>
        /// Total array count in flex array
        /// </summary>
        public int MaxArrayCount { get; }

        /// <summary>
        /// Creates new flex array.
        /// </summary>
        public FlexArray(int capacity)
        {
            if (capacity < 500)
            {
                ArraySize = capacity;
                MaxArrayCount = 1;

                CreateArrays();
                return;
            }

            ArraySize = capacity <= 50000 ? 500 : 1000;
            MaxArrayCount = Convert.ToInt32(Math.Ceiling((double) capacity / ArraySize));

            CreateArrays();
        }

        /// <summary>
        /// Creates new flex array.
        /// Flex array consists of [maxArrayCount] arrays with [arraySize] size.
        /// All arrays are not created on initially. They are created when needed.
        /// These arrays' sizes are static, so they can be enumerated async
        /// </summary>
        public FlexArray(int arraySize, int maxArrayCount)
        {
            ArraySize = arraySize;
            MaxArrayCount = maxArrayCount;

            CreateArrays();
        }

        /// <summary>
        /// Creates array objects with specified sizes
        /// </summary>
        private void CreateArrays()
        {
            _arrays = new T[MaxArrayCount][];
            _arrays[0] = new T[ArraySize];
        }

        /// <summary>
        /// Gets all items in array
        /// </summary>
        /// <returns></returns>
        public IEnumerable<T> All()
        {
            foreach (var array in _arrays)
            {
                if (array == null)
                    yield break;

                foreach (var item in array)
                {
                    if (item != null)
                        yield return item;
                }
            }
        }

        /// <summary>
        /// Add the object to the array.
        /// If array is full, returns false
        /// </summary>
        public bool Add(T item)
        {
            for (int i = 0; i < _arrays.Length; i++)
            {
                T[] array = _arrays[i];
                if (array == null)
                {
                    _arrays[i] = new T[ArraySize];
                    _arrays[i][0] = item;
                    return true;
                }

                for (int j = 0; j < array.Length; j++)
                {
                    if (array[j] == null)
                    {
                        array[j] = item;
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Removes an object from the array
        /// </summary>
        public void Remove(T item)
        {
            if (item == null)
                return;

            foreach (var array in _arrays)
            {
                if (array == null)
                    return;

                for (int j = 0; j < array.Length; j++)
                {
                    if (array[j].Equals(item))
                    {
                        array[j] = null;
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Finds the element from the array
        /// </summary>
        public T Find(Func<T, bool> predicate)
        {
            foreach (var array in _arrays)
            {
                if (array == null)
                    return null;

                foreach (var item in array)
                {
                    if (item == null)
                        continue;

                    if (predicate(item))
                        return item;
                }
            }

            return null;
        }
    }
}