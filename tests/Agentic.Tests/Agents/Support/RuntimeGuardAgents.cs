using Agentic.Agents;
using Agentic.Tests.Toolsets.Companies;
using Agentic.Toolsets;

namespace Agentic.Tests.Agents.Support;

// Generic, so the generator leaves them alone: they reach the runtime guards that cover what it cannot check

public class MisselectingAgent<TToolset> : AgentDefinition where TToolset : Toolset, new()
{
    public MisselectingAgent() => UseToolset<TToolset>(CompanyToolset.FindCompanies);
}

public class DuplicatingAgent<TToolset> : AgentDefinition where TToolset : Toolset, new()
{
    public DuplicatingAgent()
    {
        UseToolset<TToolset>();
        UseToolset<TToolset>();
    }
}

public class MissettingAgent<TSettings> : AgentDefinition where TSettings : new()
{
    public MissettingAgent() => WithToolSettings(new TSettings());
}

public class NamedAgent<TTag> : AgentDefinition
{
    public NamedAgent(string uniqueName) => WithUniqueName(uniqueName);
}
