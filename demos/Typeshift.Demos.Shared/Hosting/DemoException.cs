namespace Typeshift.Demos.Hosting;

/// <summary>
/// A failure of the demo's setup the user can fix, such as a key that is not set or an argument it cannot read:
/// <see cref="Demo.RunAsync"/> prints its message as one line instead of a stack trace.
/// </summary>
/// <param name="message">What is wrong and what to do.</param>
public sealed class DemoException(string message) : Exception(message);
