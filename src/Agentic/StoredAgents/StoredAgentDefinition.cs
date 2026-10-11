using Agentic.Agents;
using Agentic.Catalogs;
using Agentic.Json;
using Agentic.Tools;

namespace Agentic.StoredAgents;

// A stored agent made into a definition, so code and stored agents resolve through one path
internal sealed class StoredAgentDefinition : AgentDefinition
{
    public StoredAgentDefinition(StoredAgent agent, ToolCatalog catalog, AgentJson json)
    {
        SetUniqueName(agent.UniqueName);
        WithChatOptions(agent.Options.ToChatOptions());

        if (agent.Description is { } description) WithDescription(description);

        Offer(agent.Tools.Select(name => catalog.FindDefinition(name) ?? throw Unknown(agent, name)));

        foreach (var (name, stored) in agent.ToolSettings)
        {
            var tool = Tools.FirstOrDefault(offered => offered.Name == name) ?? throw Unknown(agent, name);

            if (tool.Toolset.ImportSettings(tool, stored, json) is { } settings) SetToolSettings(tool, settings);
        }
    }

    protected override IReadOnlyList<ChatOptionProperty> ConfigurableChatOptions => StoredAgentOptions.Configurable;

    private static InvalidOperationException Unknown(StoredAgent agent, ToolName tool) =>
        new($"The stored agent {agent.UniqueName} names the tool '{tool}', which this host does not offer.");
}
