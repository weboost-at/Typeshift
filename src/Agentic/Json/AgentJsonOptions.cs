using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.AI;

namespace Agentic.Json;

/// <summary>
/// How the library serializes tool arguments, results and stored settings, configured through the Options pattern.
/// </summary>
public sealed class AgentJsonOptions
{
    /// <summary>
    /// The developer's resolvers, asked first; under Native AOT they cover the tool parameter and result types.
    /// </summary>
    public IList<IJsonTypeInfoResolver> TypeInfoResolverChain { get; } = [];

    /// <summary>
    /// The options tool schemas are created with, for example a <c>TransformSchemaNode</c> hook.
    /// </summary>
    public AIJsonSchemaCreateOptions SchemaCreateOptions { get; set; } = AIJsonSchemaCreateOptions.Default;
}
