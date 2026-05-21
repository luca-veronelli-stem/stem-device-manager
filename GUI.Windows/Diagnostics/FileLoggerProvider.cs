using Microsoft.Extensions.Logging;

namespace GUI.Windows.Diagnostics;

/// <summary>
/// Thread-safe text-file <see cref="ILoggerProvider"/>. Writes one line per
/// log entry (timestamp, level tag, category, message) and flushes after each
/// write so tailing the file gives near-real-time output.
///
/// One file per process lifetime — path is fixed at construction and the
/// parent directory is created on demand. Lifetime is managed by the DI
/// container; <see cref="Dispose"/> closes the underlying stream.
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly StreamWriter _writer;
    private readonly object _gate = new();
    private bool _disposed;

    public FileLoggerProvider(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        _writer = new StreamWriter(stream) { AutoFlush = true };
        _writer.WriteLine($"# log started {DateTime.Now:O}");
    }

    public ILogger CreateLogger(string categoryName)
        => new FileLogger(categoryName, _writer, _gate, () => _disposed);

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _writer.Flush();
            _writer.Dispose();
        }
    }

    private sealed class FileLogger : ILogger
    {
        private readonly string _category;
        private readonly StreamWriter _writer;
        private readonly object _gate;
        private readonly Func<bool> _isDisposed;

        public FileLogger(string category, StreamWriter writer, object gate, Func<bool> isDisposed)
        {
            _category = category;
            _writer = writer;
            _gate = gate;
            _isDisposed = isDisposed;
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
            if (_isDisposed()) return;
            var msg = formatter(state, exception);
            var line = $"{DateTime.Now:HH:mm:ss.fff} [{LevelTag(logLevel)}] {_category}: {msg}";
            lock (_gate)
            {
                if (_isDisposed()) return;
                _writer.WriteLine(line);
                if (exception is not null) _writer.WriteLine(exception);
            }
        }

        private static string LevelTag(LogLevel level) => level switch
        {
            LogLevel.Trace => "TRCE",
            LogLevel.Debug => "DEBU",
            LogLevel.Information => "INFO",
            LogLevel.Warning => "WARN",
            LogLevel.Error => "ERRO",
            LogLevel.Critical => "CRIT",
            _ => "NONE",
        };
    }
}
