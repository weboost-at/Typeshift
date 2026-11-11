using Microsoft.Extensions.Logging;

namespace Typeshift.Tests.Testing.Fakes;

// A logger factory that keeps the warnings every logger it made wrote
public sealed class WarningLog : ILoggerFactory, ILogger
{
    private readonly List<string> _warnings = [];

    public IReadOnlyList<string> Warnings
    {
        get
        {
            lock (_warnings) return [.. _warnings];
        }
    }

    public ILogger CreateLogger(string categoryName) => this;

    public void AddProvider(ILoggerProvider provider)
    {
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
        if (logLevel != LogLevel.Warning) return;

        lock (_warnings) _warnings.Add(formatter(state, exception));
    }

    public void Dispose()
    {
    }
}
