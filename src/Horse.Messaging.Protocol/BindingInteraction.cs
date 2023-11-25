using System.ComponentModel;

namespace Horse.Messaging.Protocol;

/// <summary>
/// Options for pending acknowledge or response from binding receiver
/// </summary>
public enum BindingInteraction
{
    /// <summary>
    /// No response is pending
    /// </summary>
    [Description("none")]
    None,

    /// <summary>
    /// Receiver should respond
    /// </summary>
    [Description("response")]
    Response
}