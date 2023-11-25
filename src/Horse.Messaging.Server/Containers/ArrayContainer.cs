using System;
using System.Linq;
using System.Threading;

namespace Horse.Messaging.Server.Containers;

/// <summary>
/// Thread-safe array container.
/// Get operations are as fast as non-concurrent arrays.
/// </summary>
public class ArrayContainer<T> where T : class
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private T[] _items = new T[0];

    /// <summary>
    /// Gets all items
    /// </summary>
    public T[] All()
    {
        return _items;
    }

    /// <summary>
    /// Finds an item
    /// </summary>
    public T Find(Func<T, bool> predicate)
    {
        return _items.FirstOrDefault(predicate);
    }

    /// <summary>
    /// Returns item count
    /// </summary>
    public int Count()
    {
        return _items.Length;
    }

    /// <summary>
    /// Adds new item to container
    /// </summary>
    public void Add(T item)
    {
        _semaphore.Wait();

        try
        {
            T[] newItems = new T[_items.Length + 1];
            Array.Copy(_items, newItems, _items.Length);
            newItems[^1] = item;
            _items = newItems;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Adds new item to container
    /// </summary>
    /// <param name="item">Adding item</param>
    /// <param name="existenceComparer">If existence function returns true, operation is cancelled.</param>
    public void Add(T item, Func<T, bool> existenceComparer)
    {
        _semaphore.Wait();

        try
        {
            if (!_items.Any(existenceComparer))
            {
                T[] newItems = new T[_items.Length + 1];
                Array.Copy(_items, newItems, _items.Length);
                newItems[^1] = item;
                _items = newItems;
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Remove an item
    /// </summary>
    public void Remove(T value)
    {
        if (_items.Length == 0)
            return;
            
        _semaphore.Wait();
            
        try
        {
            T[] newItems = _items.Where(x => x != value).ToArray();
            _items = newItems;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Remove items in filter
    /// </summary>
    public void RemoveAll(Func<T, bool> predicate)
    {
        _semaphore.Wait();

        try
        {
            for (int i = 0; i < _items.Length; i++)
            {
                T item = _items[i];

                bool remove = predicate(item);
                if (remove)
                    _items[i] = null;
            }

            T[] newItems = _items.Where(x => x != null).ToArray();
            _items = newItems;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Clears all items
    /// </summary>
    public void Clear()
    {
        _items = new T[0];
    }
}