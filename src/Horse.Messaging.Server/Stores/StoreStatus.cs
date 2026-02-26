namespace Horse.Messaging.Server.Stores;

public enum StoreStatus : byte
{
    Closed = 0,
    Initializing = 1,
    Running = 2,
    Paused = 3,
    Synchronizing = 4,
    Destroyed = 5
}