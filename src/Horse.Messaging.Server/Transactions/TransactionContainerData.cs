namespace Horse.Messaging.Server.Transactions
{
    /// <summary>
    /// Each data object represents one transaction container information in transactions.json file
    /// </summary>
    public class TransactionContainerData
    {
        /// <summary>
        /// Container name
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// Transaction timeout in milliseconds
        /// </summary>
        public int Timeout { get; set; }

        /// <summary>
        /// Full type name of custom transaction handler
        /// </summary>
        public string HandlerType { get; set; }

        /// <summary>
        /// Full type name of commit transaction endpoint object
        /// </summary>
        public string CommitType { get; set; }
        
        /// <summary>
        /// String parameter for commit type constructor
        /// </summary>
        public string CommitParam { get; set; }

        /// <summary>
        /// Full type name of rollback transaction endpoint object
        /// </summary>
        public string RollbackType { get; set; }
        
        /// <summary>
        /// String parameter for rollback type constructor
        /// </summary>
        public string RollbackParam { get; set; }

        /// <summary>
        /// Full type name of timeout transaction endpoint object
        /// </summary>
        public string TimeoutType { get; set; }
        
        /// <summary>
        /// String parameter for timeout type constructor
        /// </summary>
        public string TimeoutParam { get; set; }
    }
}