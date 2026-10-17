using Microsoft.Extensions.AI;

namespace Typeshift.Samples.Agents;

internal partial class StatusAgent : Agent
{
    public StatusAgent()
    {
        // begin-snippet: hosted-mcp
        WithChatOptions()
            .WithModel("anthropic", "claude-sonnet-5-5")
            .WithTools(new HostedMcpServerTool("status_page", new Uri("https://mcp.example.com/status")));
        // end-snippet
    }
}
