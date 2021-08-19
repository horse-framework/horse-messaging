namespace Horse.Messaging.Server.Transactions
{
    /// <summary>
    /// Statuses for transactions
    /// </summary>
    public enum ServerTransactionStatus
    {
        /// <summary>
        /// Transaction has no action, not started
        /// </summary>
        None,
        
        /// <summary>
        /// Transaction began
        /// </summary>
        Begin,
        
        /// <summary>
        /// Transaction committed
        /// </summary>
        Commit,
        
        /// <summary>
        /// Transaction rolled back
        /// </summary>
        Rollback,
        
        /// <summary>
        /// Transaction timed out
        /// </summary>
        Timeout
    }
}