using System;

namespace Horse.Messaging.Server;

/// <summary>
/// Provides methods for handling messages.
/// </summary>
/// <typeparam name="T"></typeparam>
public interface IOptionsConfigurator<T>
{
    T[] Load();

    void Save();

    void Add(T item);

    T Find(Func<T, bool> predicate);

    void Remove(Func<T, bool> predicate);

    void Remove(T item);

    void Clear();
}