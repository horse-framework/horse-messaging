using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client
{
    /// <summary>
    /// Transaction status
    /// </summary>
    public enum TransactionStatus
    {
        /// <summary>
        /// Transaction is not started
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
        Rollback
    }

    /// <summary>
    /// Horse transaction
    /// </summary>
    public class HorseTransaction : IDisposable
    {
        /// <summary>
        /// Transaction name
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Unique transaction Id
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Transaction client
        /// </summary>
        public HorseClient Client { get; }

        /// <summary>
        /// Transaction Status
        /// </summary>
        public TransactionStatus Status { get; private set; }

        private MemoryStream _transactionContent = null;

        /// <summary>
        /// Creates new transaction
        /// </summary>
        /// <param name="client">Transaction client</param>
        /// <param name="name">Transaction name</param>
        public HorseTransaction(HorseClient client, string name)
        {
            Name = name;
            Client = client;
            Id = client.UniqueIdGenerator.Create();
        }

        /// <summary>
        /// Creates new transaction
        /// </summary>
        /// <param name="client">Transaction client</param>
        /// <param name="name">Transaction name</param>
        /// <param name="transactionId">Used defined transaction Id. Must be unique.</param>
        public HorseTransaction(HorseClient client, string name, string transactionId)
        {
            Name = name;
            Client = client;
            Id = transactionId;
        }

        /// <summary>
        /// Creates and begins new transaction
        /// </summary>
        /// <param name="client">Transaction client</param>
        /// <param name="name">Transaction name</param>
        public static async Task<HorseTransaction> Begin(HorseClient client, string name)
        {
            HorseTransaction transaction = new HorseTransaction(client, name);
            await transaction.Begin();

            return transaction;
        }

        /// <summary>
        /// Diposes transaction.
        /// If transaction has begun and still not committed, it will be rolled back.
        /// </summary>
        public void Dispose()
        {
            if (Status == TransactionStatus.Begin)
                Rollback().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Sets payload content
        /// </summary>
        public void SetMessageContent(byte[] data)
        {
            _transactionContent = new MemoryStream(data);
            _transactionContent.Position = 0;
        }

        /// <summary>
        /// Begins new transactions
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if transaction already began, committed or rolled back</exception>
        /// <exception cref="KeyNotFoundException">Thrown when the transaction name is not defined on server</exception>
        /// <exception cref="Exception">Thrown when begin operation is failed</exception>
        public Task Begin()
        {
            return Begin<object>(null);
        }

        /// <summary>
        /// Begins new transactions
        /// </summary>
        /// <param name="model">Transaction payload model, if exists</param>
        /// <typeparam name="TModel">Transaction payload model type</typeparam>
        /// <exception cref="InvalidOperationException">Thrown if transaction already began, committed or rolled back</exception>
        /// <exception cref="KeyNotFoundException">Thrown when the transaction name is not defined on server</exception>
        /// <exception cref="Exception">Thrown when begin operation is failed</exception>
        public async Task Begin<TModel>(TModel model)
        {
            if (Status != TransactionStatus.None)
                throw new InvalidOperationException($"{Name} Transaction already began ({Id})");

            HorseMessage message = new HorseMessage(MessageType.Transaction, Name, KnownContentTypes.TransactionBegin);
            message.SetMessageId(Id);

            if (model != null)
                Client.MessageSerializer.Serialize(message, model);
            else if (_transactionContent != null)
                message.Content = _transactionContent;

            HorseMessage response = await Client.Request(message);
            HorseResultCode code = (HorseResultCode) response.ContentType;

            if (code == HorseResultCode.Ok)
            {
                Status = TransactionStatus.Begin;
                return;
            }

            if (code == HorseResultCode.NotFound)
                throw new KeyNotFoundException("Transaction Name is not defined in server: " + Name);

            if (code == HorseResultCode.Failed)
                throw new Exception($"{Name} transaction begin operation is failed ({message.MessageId})");
        }

        /// <summary>
        /// Commits a transaction.
        /// Successful result returns true
        /// </summary>
        public async Task<bool> Commit()
        {
            if (Status != TransactionStatus.Begin)
                throw new InvalidOperationException($"{Name} Transaction in {Status} status cannot be commited ({Id})");

            HorseMessage message = new HorseMessage(MessageType.Transaction, Name, KnownContentTypes.TransactionCommit);
            message.SetMessageId(Id);

            HorseMessage response = await Client.Request(message);
            HorseResultCode code = (HorseResultCode) response.ContentType;

            bool success = code == HorseResultCode.Ok;
            if (success)
                Status = TransactionStatus.Commit;

            return success;
        }

        /// <summary>
        /// Rollbacks a transaction.
        /// Successful result returns true
        /// </summary>
        public async Task<bool> Rollback()
        {
            if (Status != TransactionStatus.Begin)
                throw new InvalidOperationException($"{Name} Transaction in {Status} status cannot be rolled back ({Id})");

            HorseMessage message = new HorseMessage(MessageType.Transaction, Name, KnownContentTypes.TransactionRollback);
            message.SetMessageId(Id);

            HorseMessage response = await Client.Request(message);
            HorseResultCode code = (HorseResultCode) response.ContentType;

            bool success = code == HorseResultCode.Ok;
            if (success)
                Status = TransactionStatus.Rollback;

            return success;
        }
    }
}