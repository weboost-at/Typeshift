using Agentic.Agents;
using Agentic.Toolsets;

namespace Agentic.Tests.Agents.Support;

// Generic, so the generator leaves them alone: they reach the runtime guards that cover what it cannot check

public class MisselectingAgent<TToolset> : AgentDefinition where TToolset : Toolset, new()
{
    public MisselectingAgent() => UseToolset<TToolset>(tools => tools.Select("find_companies"));
}

public class DuplicatingAgent<TToolset> : AgentDefinition where TToolset : Toolset, new()
{
    public DuplicatingAgent()
    {
        UseToolset<TToolset>();
        UseToolset<TToolset>();
    }
}
