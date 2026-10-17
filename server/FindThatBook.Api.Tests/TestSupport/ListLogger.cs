using Microsoft.Extensions.Logging;

namespace FindThatBook.Api.Tests.TestSupport;

/// <summary>A logger that keeps every entry in memory so tests can check what was logged.</summary>
public class ListLogger<T> : ILogger<T>
{
    public List<LogEntry> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        Entries.Add(new LogEntry { Level = logLevel, Message = formatter(state, exception) });
}

public class LogEntry
{
    public required LogLevel Level { get; init; }
    public required string Message { get; init; }
}
