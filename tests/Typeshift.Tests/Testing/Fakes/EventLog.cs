using Microsoft.Extensions.Logging;

namespace Typeshift.Tests.Testing.Fakes;

// A logger factory that keeps every entry its loggers wrote, at any level, each with its category and its event
public sealed class EventLog : ILoggerFactory
{
    private readonly List<LoggedEvent> _events = [];

    public IReadOnlyList<LoggedEvent> Events
    {
        get
        {
            lock (_events) return [.. _events];
        }
    }

    // The entries of one event, by the name its id carries
    public IReadOnlyList<LoggedEvent> Of(string name) => [.. Events.Where(logged => logged.Id.Name == name)];

    public ILogger CreateLogger(string categoryName) => new Logger(this, categoryName);

    public void AddProvider(ILoggerProvider provider)
    {
    }

    public void Dispose()
    {
    }

    private sealed class Logger(EventLog log, string category) : ILogger
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
            var logged = new LoggedEvent(category, logLevel, eventId, formatter(state, exception), exception);

            lock (log._events) log._events.Add(logged);
        }
    }
}

public sealed record LoggedEvent(string Category, LogLevel Level, EventId Id, string Message, Exception? Exception);
