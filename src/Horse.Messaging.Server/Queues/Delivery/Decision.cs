namespace Horse.Messaging.Server.Queues.Delivery
{
    /// <summary>
    /// Decision description for each step in message delivery
    /// </summary>
    public readonly struct Decision
    {
        /// <summary>
        /// If true, queue message operation will be interrupted.
        /// </summary>
        public readonly bool Interrupt;

        /// <summary>
        /// If true, message will be saved.
        /// If message already saved, second save will be discarded.
        /// </summary>
        public readonly bool Save;

        /// <summary>
        /// If true, message will be deleted.
        /// If message already deleted, second save will be discarded.
        /// </summary>
        public readonly bool Delete;

        /// <summary>
        /// If not No, message will be kept in front of the queue.
        /// Settings this value always true may cause infinity same message send operation.
        /// </summary>
        public readonly PutBackDecision PutBack;

        /// <summary>
        /// Sending decision message to producer
        /// </summary>
        public readonly DecisionTransmission Transmission;

        /// <summary>
        /// Creates new decision with full parameters
        /// </summary>
        internal Decision(bool interrupt, bool save, bool delete, PutBackDecision putBack, DecisionTransmission transmission)
        {
            Interrupt = interrupt;
            Save = save;
            Delete = delete;
            PutBack = putBack;
            Transmission = transmission;
        }

        /// <summary>
        /// Does nothing special. Just decision for moving to next handler step.
        /// </summary>
        public static Decision NoveNext(DecisionTransmission transmission = DecisionTransmission.None)
        {
            return new Decision(false, false, false, PutBackDecision.No, transmission);
        }

        /// <summary>
        /// Sends commit or failed message to producer
        /// </summary>
        public static Decision TransmitToProducer(DecisionTransmission transmission)
        {
            return new Decision(false, false, false, PutBackDecision.No, transmission);
        }

        /// <summary>
        /// Creates new message remove decision
        /// </summary>
        /// <returns></returns>
        public static Decision DeleteMessage(DecisionTransmission transmission = DecisionTransmission.None)
        {
            return new Decision(false, false, true, PutBackDecision.No, transmission);
        }

        /// <summary>
        /// Saves the message.
        /// If sendCommit is true, commit message is sent to producer
        /// </summary>
        public static Decision SaveMessage(DecisionTransmission transmission = DecisionTransmission.None)
        {
            return new Decision(false, true, false, PutBackDecision.No, transmission);
        }

        /// <summary>
        /// Interrupts queue operation flow.
        /// If delete parameter is true, message will be deleted.
        /// </summary>
        public static Decision InterruptFlow(bool deleteMessage, DecisionTransmission transmission = DecisionTransmission.None)
        {
            return new Decision(true, false, deleteMessage, PutBackDecision.No, transmission);
        }

        /// <summary>
        /// Puts the message back to the queue
        /// </summary>
        public static Decision PutBackMessage(bool toEndOfQueue, DecisionTransmission transmission = DecisionTransmission.None)
        {
            return new Decision(false, false, false, toEndOfQueue ? PutBackDecision.Regular : PutBackDecision.Priority, transmission);
        }
    }
}