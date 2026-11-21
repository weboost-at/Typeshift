using Typeshift.Tests.Tools.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Tests.Agents.Support;

// Builds its instructions from the run's services, so one instance of the agent tells each scope apart
public partial class ScopedInstructionsAgent : Agent
{
    public ScopedInstructionsAgent() =>
        WithChatOptions()
            .WithProvider(ProbeAgent.ModelProvider)
            .WithModel(ProbeAgent.Model);

    public static string For(Guid scope) => $"Answer the caller of scope {scope}.";

    protected override string GetInstructions(IServiceProvider services) =>
        For(services.GetRequiredService<CallScope>().Id);
}
