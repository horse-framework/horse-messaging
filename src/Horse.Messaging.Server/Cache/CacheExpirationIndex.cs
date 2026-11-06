using System;
using System.Collections.Generic;

namespace Horse.Messaging.Server.Cache;

/// <summary>
/// Indexed min-heap. The owning cache partition serializes access. Replacing a
/// key replaces its heap entry, so repeated long-lived writes cannot accumulate
/// obsolete values until their expiration dates.
/// </summary>
internal sealed class CacheExpirationIndex
{
    private readonly List<HorseCacheItem> _heap = new();
    private readonly Dictionary<string, int> _positions = new(StringComparer.OrdinalIgnoreCase);

    public bool TryPeek(out HorseCacheItem item)
    {
        item = _heap.Count == 0 ? null : _heap[0];
        return item != null;
    }

    public void Set(HorseCacheItem item)
    {
        if (_positions.TryGetValue(item.Key, out int index))
            _heap[index] = item;
        else
        {
            index = _heap.Count;
            _positions.Add(item.Key, index);
            _heap.Add(item);
        }

        Repair(index);
    }

    public void Remove(string key)
    {
        if (!_positions.Remove(key, out int index))
            return;

        int last = _heap.Count - 1;
        if (index == last)
        {
            _heap.RemoveAt(last);
            return;
        }

        HorseCacheItem replacement = _heap[last];
        _heap[index] = replacement;
        _positions[replacement.Key] = index;
        _heap.RemoveAt(last);
        Repair(index);
    }

    public void Clear()
    {
        _heap.Clear();
        _positions.Clear();
    }

    private void Repair(int index)
    {
        if (index > 0 && _heap[index].Expiration < _heap[(index - 1) / 2].Expiration)
        {
            while (index > 0)
            {
                int parent = (index - 1) / 2;
                if (_heap[parent].Expiration <= _heap[index].Expiration)
                    break;
                Swap(parent, index);
                index = parent;
            }
        }
        else
        {
            while (index * 2 + 1 < _heap.Count)
            {
                int child = index * 2 + 1;
                if (child + 1 < _heap.Count && _heap[child + 1].Expiration < _heap[child].Expiration)
                    child++;
                if (_heap[index].Expiration <= _heap[child].Expiration)
                    break;
                Swap(index, child);
                index = child;
            }
        }
    }

    private void Swap(int a, int b)
    {
        (_heap[a], _heap[b]) = (_heap[b], _heap[a]);
        _positions[_heap[a].Key] = a;
        _positions[_heap[b].Key] = b;
    }
}
