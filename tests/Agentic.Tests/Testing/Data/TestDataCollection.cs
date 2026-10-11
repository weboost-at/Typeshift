using Agentic.Tests.Tools.Support;
using Bogus;

namespace Agentic.Tests.Testing.Data;

// One property per area; every base exposes it as Data
public class TestDataCollection(Faker faker)
{
    public ToolTestData Tool { get; } = new(faker);
}
