namespace Twino.MQ.Client.Models
{
    /// <summary>
    /// In order to use models on PushException or PublishException, they must implement that interface.
    /// The model will be pushed or published after Initialize method is called.
    /// </summary>
    public interface ITransportableException
    {
        /// <summary>
        /// Initializes transportable exception model
        /// </summary>
        void Initialize(ExceptionContext context);
    }
}