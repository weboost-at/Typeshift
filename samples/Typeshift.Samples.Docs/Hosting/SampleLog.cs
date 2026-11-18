using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Typeshift.Samples.Hosting;

// Keeps what the tool middleware logs, so the scenarios see which calls reached it
internal sealed class SampleLog : ILoggerProvider
{
    private readonly ConcurrentQueue<string> _audited = [];

    public IReadOnlyCollection<string> Audited => _audited;

    public ILogger CreateLogger(string categoryName) =>
        categoryName == typeof(AuditToolCalls).FullName ? new Audit(_audited) : NullLogger.Instance;

    public void Dispose()
    {
    }

    private sealed class Audit(ConcurrentQueue<string> audited) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            audited.Enqueue(formatter(state, exception));
    }
}
