using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Typeshift.Generated;

namespace Typeshift;

// D33, D65: a sub-agent as a tool: its answer's text or typed JSON is the result, or, in the background, a note that
// it started
internal sealed class SubAgentFunction(AgentUse use, Agent agent) : AIFunction
{
    private static readonly JsonElement Schema = JsonElement.Parse(
        """
        {
          "type": "object",
          "properties": { "input": { "type": "string", "description": "What to ask or tell the agent." } },
          "required": ["input"]
        }
        """);

    public override string Name => agent.Name;

    public override string Description => agent.Description ?? "";

    public override JsonElement JsonSchema => Schema;

    protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken ct)
    {
        var context = FunctionInvokingChatClient.CurrentContext;

        if (context?.Options is not ResolvedChatOptions { AgentRun: { } caller, ConversationRun: { } run } options)
            throw new InvalidOperationException($"The sub-agent {Name} runs as a tool of an agent's run.");

        var input = new ChatMessage(ChatRole.User, Input(arguments));
        var host = use.Host(caller.Services);
        var json = caller.Services.GetRequiredService<ToolJson>();
        var format = use.Result?.GetFormat(json);

        // D33: the caller's settings id and the caller itself, and no conversation of its own
        var handover = new RunHandover(options.SettingsId, Conversation: null, Claim: null, format, caller);

        Task<ChatResponse> RunAsync(CancellationToken token) => host.RunAsync([input], handover, token);

        if (!use.Background) return Answer(await RunAsync(ct), json);

        run.StartBackground(Name, context.CallContent.CallId, async stop => Text(Answer(await RunAsync(stop), json)));

        return $"{Name} works in the background; its answer arrives as a message once it is done.";
    }

    // D65: a typed answer reaches the calling model as its JSON, an answer of the wrong shape as the call's failure
    private object Answer(ChatResponse response, ToolJson json) =>
        use.Result is { } result ? result.Read(response, json, Name) : response.Text;

    // A background answer arrives as a message, so a typed one as its JSON's text
    private static string Text(object answer) => answer is JsonElement element ? element.GetRawText() : (string)answer;

    private static string Input(AIFunctionArguments arguments) =>
        arguments.TryGetValue("input", out var value)
            ? value switch
            {
                JsonElement { ValueKind: JsonValueKind.String } text => text.GetString()!,
                string text => text,
                _ => Tokens.Text(value)
            }
            : "";
}
