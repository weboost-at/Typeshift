namespace Typeshift.Agents;

/// <summary>
/// Thrown when an agent cannot run before someone configures it, for example with a model.
/// </summary>
/// <param name="message">What is missing and where to set it.</param>
public sealed class ConfigurationRequiredException(string message) : InvalidOperationException(message);
