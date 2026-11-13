using Microsoft.Extensions.Logging;

namespace Typeshift.Tests.Persistence.Support;

// A logger that keeps what was written at Debug, which the warnings' log leaves out
public sealed class DebugLog : ILogger
{
    private readonly List<string> _messages = [];

    public IReadOnlyList<string> Messages
    {
        get
        {
            lock (_messages) return [.. _messages];
        }
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (logLevel != LogLevel.Debug) return;

        lock (_messages) _messages.Add(formatter(state, exception));
    }
}
