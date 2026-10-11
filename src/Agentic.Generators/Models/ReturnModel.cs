namespace Agentic.Generators.Models;

internal enum ReturnKind
{
    Void,
    Value,
    Awaitable,
    AwaitableValue
}

// Result is the type of the value the tool produces, after awaiting
internal sealed record ReturnModel(ReturnKind Kind, string? Result);
