using Microsoft.Extensions.AI;

namespace Typeshift.Samples.Hosting;

// Stands in for the MCP servers, so the guide's scenarios run without a network
internal sealed class CannedMcpToolProvider : IMcpToolProvider
{
    private static readonly AIFunction Status =
        AIFunctionFactory.Create(() => "All systems operational.", "service_status", "The status of our services.");

    public ValueTask<IReadOnlyList<AITool>> GetToolsAsync(string server, CancellationToken ct) =>
        ValueTask.FromResult<IReadOnlyList<AITool>>([Status]);
}
