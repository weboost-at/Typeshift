using Microsoft.Extensions.AI;

namespace Typeshift.Samples.Agents;

internal partial class StatusAgent : Agent
{
    public StatusAgent()
    {
        // begin-snippet: hosted-mcp
        WithChatOptions()
            .WithModel("reasoning-model")
            .WithTools(new HostedMcpServerTool("status_page", new Uri("https://mcp.example.com/status")));
        // end-snippet
    }
}
