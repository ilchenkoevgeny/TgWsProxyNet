namespace TgWsProxy.Logging;

/// <summary>
/// Adds a minimal file logger without external dependencies.
/// </summary>
public static class SimpleFileLoggerExtensions
{
    /// <summary>
    /// Adds a file logger if a path is configured.
    /// </summary>
    public static ILoggingBuilder AddSimpleFile(this ILoggingBuilder builder, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return builder;
        }

        builder.AddProvider(new SimpleFileLoggerProvider(path));
        return builder;
    }
}
