namespace Agentic.Agents;

/// <summary>
/// Thrown when an agent cannot run before someone configures it, for example with a model.
/// </summary>
public sealed class ConfigurationRequiredException(string message) : InvalidOperationException(message);
