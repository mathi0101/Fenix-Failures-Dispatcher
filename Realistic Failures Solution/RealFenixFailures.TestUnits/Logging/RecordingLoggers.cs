using Microsoft.Extensions.Logging;

namespace RealFenixFailures.TestUnits.Logging;

public sealed record LogEntry(LogLevel Level, string Message, string CategoryName);

public abstract class RecordingLoggerBase : ILogger {
    private readonly List<LogEntry> _entries;

    protected RecordingLoggerBase(List<LogEntry> entries, string categoryName) {
        _entries = entries;
        CategoryName = categoryName;
    }

    protected string CategoryName { get; }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
        _entries.Add(new LogEntry(logLevel, formatter(state, exception), CategoryName));
    }
}

public sealed class RecordingLogger : RecordingLoggerBase {
    public RecordingLogger(List<LogEntry> entries, string categoryName) : base(entries, categoryName) { }
}

public sealed class RecordingLogger<T> : RecordingLoggerBase, ILogger<T> where T : class {
    public RecordingLogger(List<LogEntry> entries) : base(entries, typeof(T).FullName!) { }
}

public sealed class RecordingLoggerFactory : ILoggerFactory {
    private readonly List<LogEntry> _entries;

    public RecordingLoggerFactory(List<LogEntry>? sharedEntries = null) => _entries = sharedEntries ?? new List<LogEntry>();

    public IReadOnlyList<LogEntry> Entries => _entries;

    public ILogger CreateLogger(string categoryName) => new RecordingLogger(_entries, categoryName);

    public void AddProvider(ILoggerProvider provider) { }

    public void Dispose() { }
}
