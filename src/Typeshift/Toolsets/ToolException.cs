namespace Typeshift;

/// <summary>
/// A failed tool call whose message is written for the model. The model reads the message as the call's result and
/// can correct its call. Of any other exception a tool throws the model reads only that the call failed.
/// </summary>
public class ToolException : Exception
{
    /// <summary>
    /// Creates the failure.
    /// </summary>
    /// <param name="message">What the model reads: what went wrong, and what to do instead.</param>
    public ToolException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Creates the failure from the exception behind it.
    /// </summary>
    /// <param name="message">What the model reads: what went wrong, and what to do instead.</param>
    /// <param name="innerException">The exception behind the failure, which the model never reads.</param>
    public ToolException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
