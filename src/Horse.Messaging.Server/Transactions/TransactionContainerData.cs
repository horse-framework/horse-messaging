namespace Horse.Messaging.Server.Transactions;

/// <summary>
/// Each data object represents one transaction container information in transactions.json file.
/// Type names are saved as "Namespace.TypeName, AssemblyName".
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
    /// Type name of custom transaction handler.
    /// Null if the container uses the rider's default handler.
    /// The type must have a parameterless constructor.
    /// </summary>
    public string HandlerType { get; set; }

    /// <summary>
    /// Type name of commit transaction endpoint object
    /// </summary>
    public string CommitType { get; set; }

    /// <summary>
    /// String parameter for commit type constructor
    /// </summary>
    public string CommitParam { get; set; }

    /// <summary>
    /// Type name of rollback transaction endpoint object
    /// </summary>
    public string RollbackType { get; set; }

    /// <summary>
    /// String parameter for rollback type constructor
    /// </summary>
    public string RollbackParam { get; set; }

    /// <summary>
    /// Type name of timeout transaction endpoint object.
    /// Null if the container has no timeout endpoint.
    /// </summary>
    public string TimeoutType { get; set; }

    /// <summary>
    /// String parameter for timeout type constructor
    /// </summary>
    public string TimeoutParam { get; set; }
}
