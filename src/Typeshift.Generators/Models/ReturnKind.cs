namespace Typeshift.Generators.Models;

// What a tool method returns, which decides how the generated function awaits and wraps it. Content is an AIContent
// or a sequence of them, which the model receives as it is
internal enum ReturnKind
{
    Void,
    Value,
    Awaitable,
    AwaitableValue,
    Stream,
    Content,
    AwaitableContent
}
