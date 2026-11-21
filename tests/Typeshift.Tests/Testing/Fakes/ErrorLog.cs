using Microsoft.Extensions.Logging;

namespace Typeshift.Tests.Testing.Fakes;

// A logger factory that keeps the exceptions its loggers logged as errors, each with its logger's category
public sealed class ErrorLog : ILoggerFactory
{
    private readonly List<(string Category, Exception Exception)> _errors = [];

    public IReadOnlyList<(string Category, Exception Exception)> Errors
    {
        get
        {
            lock (_errors) return [.. _errors];
        }
    }

    public ILogger CreateLogger(string categoryName) => new Logger(this, categoryName);

    public void AddProvider(ILoggerProvider provider)
    {
    }

    public void Dispose()
    {
    }

    private sealed class Logger(ErrorLog log, string category) : ILogger
    {
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

            lock (log._errors) log._errors.Add((category, exception));
        }
    }
}
