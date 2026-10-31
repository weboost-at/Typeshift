using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Typeshift;

// D33: a sub-agent as a tool: its answer's text is the result, or, in the background, a note that it started
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

        if (!use.Background) return (await host.RunAsync([input], options.SettingsId, null, caller, null, ct)).Text;

        run.StartBackground(
            Name,
            context.CallContent.CallId,
            stop => host.RunAsync([input], options.SettingsId, null, caller, null, stop));

        return $"{Name} works in the background; its answer arrives as a message once it is done.";
    }

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
