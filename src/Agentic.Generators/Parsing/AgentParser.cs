using System.Linq;
using System.Threading;
using Agentic.Generators.Models;
using Microsoft.CodeAnalysis;

namespace Agentic.Generators.Parsing;

// Reads the declaration chain of an agent's constructors
internal static class AgentParser
{
    public static AgentModel Parse(INamedTypeSymbol agent, Compilation compilation, CancellationToken ct)
    {
        var uniqueName = ConstructorChains.GetInvocations(agent, compilation, ct)
            .Where(invocation => invocation.TargetMethod.Name == "WithUniqueName"
                && invocation.TargetMethod.ContainingType.Is(KnownTypes.AgentDefinition))
            .Select(ConstructorChains.GetConstantString)
            .LastOrDefault();

        return new AgentModel(TypeModels.From(agent), uniqueName, LocationInfo.From(agent.Locations.FirstOrDefault()));
    }
}
