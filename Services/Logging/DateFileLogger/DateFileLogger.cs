namespace AudioSnapServer.Services.DateFileLogger;

public class DateFileLogger : ILogger
{
    private string _logFilePath;

    public DateFileLogger(string logDirPath, string logFileName)
    {
        _logFilePath = logDirPath + logFileName;
    }

    // null-forgiving operator is used in a
    // documentation example. 
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => default!;

    public bool IsEnabled(LogLevel logLevel)
    {
        // The logger enables all types of logLevel, 
        // there isn't anything special about a particular
        // logLevel, like color map (as in MSDN)
        return true;
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        // in case something eventually comes up
        if (IsEnabled(logLevel))
        {
            using (StreamWriter fileWriter = new StreamWriter(_logFilePath, true))
            {
                fileWriter.WriteLine($"[ {DateTime.Now:MM/dd/yyyy HH:mm:ss zzz} ][ {logLevel} ]");
                fileWriter.WriteLine(formatter(state, exception));
                fileWriter.WriteLine();
                fileWriter.Flush();
            }
        }
    }
}