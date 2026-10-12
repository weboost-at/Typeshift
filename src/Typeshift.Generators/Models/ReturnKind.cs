namespace Typeshift.Generators.Models;

// What a tool method returns, which decides how the generated function awaits and wraps it
internal enum ReturnKind
{
    Void,
    Value,
    Awaitable,
    AwaitableValue
}
