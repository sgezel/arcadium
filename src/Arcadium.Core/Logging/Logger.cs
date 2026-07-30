using Arcadium.Core.Models;

namespace Arcadium.Core.Logging;

public static class Logger
{
    private static ILogger? _logger;

    public static void Initialize(ILogger logger)
    {
        _logger = logger;
    }

    public static void Initialize(LogType logType)
    {
        switch (logType)
        {
            case LogType.Console:
                _logger = new ConsoleLogger();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(logType), logType, null);
        }
    }

    public static void LogInformation(string message)
    {
        _logger?.LogInformation(message);
    }

    public static void LogWarning(string message)
    {
        _logger?.LogWarning(message);
    }

    public static void LogError(string message, Exception? exception = null)
    {
        _logger?.LogError(message, exception);
    }
}
