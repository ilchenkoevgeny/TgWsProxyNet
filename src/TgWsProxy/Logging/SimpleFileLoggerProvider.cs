using System.Collections.Concurrent;

namespace TgWsProxy.Logging;

/// <summary>
/// Simple append-only file logger provider.
/// </summary>
public sealed class SimpleFileLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentDictionary<string, SimpleFileLogger> loggers = new(StringComparer.OrdinalIgnoreCase);
    private readonly StreamWriter writer;
    private readonly object syncRoot = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="SimpleFileLoggerProvider"/> class.
    /// </summary>
    public SimpleFileLoggerProvider(string path)
    {
        var fullPath = Path.IsPathRooted(path)
            ? path
            : Path.Combine(AppContext.BaseDirectory, path);

        var directory = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        writer = new StreamWriter(new FileStream(fullPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
        {
            AutoFlush = true
        };
    }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName)
    {
        return loggers.GetOrAdd(categoryName, name => new SimpleFileLogger(name, writer, syncRoot));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        writer.Dispose();
    }
}
