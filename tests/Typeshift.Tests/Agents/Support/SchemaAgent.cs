using Typeshift.Tests.Tools.Support;

namespace Typeshift.Tests.Agents.Support;

// Named, without a model, and offering all but one tool of a toolset
public partial class SchemaAgent : Agent
{
    public const string AgentName = "schema";

    public SchemaAgent()
    {
        WithName(AgentName);

        UseToolset<SchemaToolset>().Exclude(t => t.EchoJson);
    }
}
