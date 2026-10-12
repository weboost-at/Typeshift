using Typeshift.Tests.Agents.Support;
using Typeshift.Tests.Conversations.Support;
using Typeshift.Tests.Persistence.Support;
using Typeshift.Tests.Tools.Support;
using Bogus;

namespace Typeshift.Tests.Testing.Data;

// One property per area; every base exposes it as Data
public class TestDataCollection(Faker faker)
{
    public ToolTestData Tool { get; } = new(faker);

    public AgentTestData Agent { get; } = new(faker);

    public ConversationTestData Conversation { get; } = new(faker);

    public StorableTestData Storable { get; } = new(faker);
}
