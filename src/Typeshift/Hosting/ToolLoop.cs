using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Typeshift;

// D53: the tool loop of a run. A failed call tells the model why only when the message was written for it, as a
// ToolException's is. Any other exception stays what the loop threw: it is logged, set on the call's span, counted
// by MaxConsecutiveToolErrors and, past that limit, thrown to the app as it is
internal sealed class ToolLoop(IChatClient inner, ILoggerFactory? loggers, IServiceProvider services)
    : FunctionInvokingChatClient(inner, loggers, services)
{
    public const string Undisclosed = "The details of the failure are not available to you.";

    public const string Failed = $"Error: Function failed. {Undisclosed}";

    protected override IList<ChatMessage> CreateResponseMessages(ReadOnlySpan<FunctionInvocationResult> results)
    {
        var messages = base.CreateResponseMessages(results);

        foreach (var result in messages.SelectMany(message => message.Contents).OfType<FunctionResultContent>())
        {
            if (result.Exception is not (null or ToolException)) result.Result = Failed;
        }

        return messages;
    }
}
