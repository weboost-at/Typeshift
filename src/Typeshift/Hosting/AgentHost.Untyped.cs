namespace Typeshift;

// D83: the copies of a host whose agent's type the caller does not name are hosts of that kind too
public sealed partial class AgentHost<TAgent>
{
    IAgentHost IAgentHost.WithSettingsId<TId>(TId id) => WithSettingsId(id);

    IAgentHost IAgentHost.WithSettingsId<TId>(TId? id) where TId : struct => WithSettingsId(id);

    IAgentHost IAgentHost.WithConversationId<TId>(TId id) => WithConversationId(id);

    IAgentHost IAgentHost.WithConversationId<TId>(TId? id) where TId : struct => WithConversationId(id);

    IAgentHost IAgentHost.WithProvider<TProviderId>(TProviderId provider) => WithProvider(provider);

    IAgentHost IAgentHost.WithProvider(ChatProvider provider) => WithProvider(provider);

    IAgentHost IAgentHost.WithModel(string model) => WithModel(model);
}
