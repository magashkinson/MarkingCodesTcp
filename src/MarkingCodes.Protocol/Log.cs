namespace MarkingCodes.Protocol;

public static class Log
{
    public static void Info(string message) => Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} {message}");
}
