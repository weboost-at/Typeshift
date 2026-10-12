namespace Typeshift.Tests.Toolsets.Support;

// Registered scoped where a test needs to tell tool calls apart by their DI scope
public sealed class CallScope
{
    public Guid Id { get; } = Guid.NewGuid();
}
