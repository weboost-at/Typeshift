using System.ComponentModel;

namespace Typeshift.Tests.Agents.Support;

// A result type that names and describes the schema the model is asked to answer in
[DisplayName(NamedFinding.Name)]
[Description(NamedFinding.About)]
public sealed record NamedFinding(string Summary)
{
    public const string Name = "ticket_finding";

    public const string About = "What the agent found out about a ticket.";
}
