using Microsoft.Extensions.Logging;

namespace Typeshift.Tests.Testing.Fakes;

// A logger factory that keeps the exceptions every logger it made logged as errors
public sealed class ErrorLog : ILoggerFactory, ILogger
{
    private readonly List<Exception> _exceptions = [];

    public IReadOnlyList<Exception> Exceptions
    {
        get
        {
            lock (_exceptions) return [.. _exceptions];
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
        if (logLevel != LogLevel.Error || exception is null) return;

        lock (_exceptions) _exceptions.Add(exception);
    }

    public void Dispose()
    {
    }
}
