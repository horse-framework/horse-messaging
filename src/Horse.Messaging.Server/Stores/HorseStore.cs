namespace Horse.Messaging.Server.Stores;

public class HorseStore
{
    public string Name { get; private set; }
    public StoreStatus Status { get; private set; }
    private StorePartition[] _partitions = [];
    
    public void Load()
    {
        /*
         * load store file
         * - store file includes partition count
         * - partition offsets
         * - partition file min and max offset times
         * - client name offsets
         */
    }
}