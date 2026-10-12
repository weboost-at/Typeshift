using Agentic.Tests.Agents.Support;
using Agentic.Tests.Conversations.Support;
using Agentic.Tests.Persistence.Support;
using Agentic.Tests.Tools.Support;
using Bogus;

namespace Agentic.Tests.Testing.Data;

// One property per area; every base exposes it as Data
public class TestDataCollection(Faker faker)
{
    public ToolTestData Tool { get; } = new(faker);

    public AgentTestData Agent { get; } = new(faker);

    public ConversationTestData Conversation { get; } = new(faker);

    public StorableTestData Storable { get; } = new(faker);
}
