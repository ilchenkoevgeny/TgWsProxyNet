namespace TgWsProxy.Logging;

/// <summary>
/// Simple append-only file logger.
/// </summary>
public sealed class SimpleFileLogger : ILogger
{
    private readonly string categoryName;
    private readonly StreamWriter writer;
    private readonly object syncRoot;

    /// <summary>
    /// Initializes a new instance of the <see cref="SimpleFileLogger"/> class.
    /// </summary>
    public SimpleFileLogger(string categoryName, StreamWriter writer, object syncRoot)
    {
        this.categoryName = categoryName;
        this.writer = writer;
        this.syncRoot = syncRoot;
    }

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        return null;
    }

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel != LogLevel.None;
    }

    /// <inheritdoc />
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

        var message = formatter(state, exception);

        if (string.IsNullOrWhiteSpace(message) && exception is null)
        {
            return;
        }

        lock (syncRoot)
        {
            writer.Write(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz"));
            writer.Write(' ');
            writer.Write(logLevel.ToString().ToUpperInvariant().PadRight(11));
            writer.Write(' ');
            writer.Write(categoryName);
            writer.Write(" - ");
            writer.WriteLine(message);

            if (exception is not null)
            {
                writer.WriteLine(exception);
            }
        }
    }
}
