using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Faber.AppHost.Tests;

/// <summary>Keeps every message logged at warning level or above, for assertions.</summary>
internal sealed class CollectingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<string> _warnings = new();

    public IReadOnlyCollection<string> Warnings => _warnings;

    public ILogger CreateLogger(string categoryName) => new CollectingLogger(_warnings);

    public void Dispose()
    {
    }

    private sealed class CollectingLogger(ConcurrentQueue<string> warnings) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            warnings.Enqueue(formatter(state, exception));
        }
    }
}
