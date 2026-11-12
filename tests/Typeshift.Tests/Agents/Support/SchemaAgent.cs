using Typeshift.Tests.Tools.Support;

namespace Typeshift.Tests.Agents.Support;

// Named, without a model, and offering all but one tool of a toolset
/// <summary>
/// Answers questions about the schema tools.
/// </summary>
public partial class SchemaAgent : Agent
{
    public const string AgentName = "schema";

    public const string Summary = "Answers questions about the schema tools.";

    public SchemaAgent()
    {
        WithName(AgentName);

        UseToolset<SchemaToolset>().Exclude(t => t.EchoJson);
    }
}
