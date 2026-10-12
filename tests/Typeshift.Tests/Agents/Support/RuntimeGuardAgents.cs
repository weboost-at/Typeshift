using Typeshift.Agents;
using Typeshift.Tools;
using Typeshift.Toolsets;

namespace Typeshift.Tests.Agents.Support;

// Generic, so the generator leaves them alone: they reach the runtime guards that cover what it cannot check

public class MisselectingAgent<TToolset> : AgentDefinition where TToolset : Toolset, new()
{
    public MisselectingAgent() => UseToolset<TToolset>().Include(_ => new ToolRef("find_companies"));
}

public class DuplicatingAgent<TToolset> : AgentDefinition where TToolset : Toolset, new()
{
    public DuplicatingAgent()
    {
        UseToolset<TToolset>();
        UseToolset<TToolset>();
    }
}
