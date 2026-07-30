namespace Arcadium.Core.Logging;

public class ConsoleLogger : ILogger
{
    public void LogInformation(string message)
    {
        Console.WriteLine($"[INFO] {message}");
    }

    public void LogWarning(string message)
    {
        Console.WriteLine($"[WARN] {message}");
    }

    public void LogError(string message, Exception? exception = null)
    {
        Console.Error.WriteLine($"[ERROR] {message}");
        if (exception != null)
        {
            Console.Error.WriteLine(exception);
        }
    }
}
