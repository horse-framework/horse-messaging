using System;
using System.Threading.Tasks;

namespace Horse.Messaging.Server;

public interface IPersistenceConfigurator<T>
{
    Task<T[]> Load();

    Task<bool> Save();

    void Add(T item);

    T Find(Func<T, bool> predicate);

    void Remove(Func<T, bool> predicate);

    void Remove(T item);

    void Clear();
}