namespace AudioSnapServer.Services.DateFileLogger;

public class DateFileLogger : ILogger
{
    // private static ReaderWriterLockSlim _readWriteLock = new ReaderWriterLockSlim();
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
            string logTime = $"{DateTime.Now:MM/dd/yyyy HH:mm:ss zzz}";
            string logContent = formatter(state, exception);
            
            // Fixed thread-safety (wow, logging isn't thread safe, alright).
            // A bad example of making it, a better idea would've been to make
            // some kind of buffer to store the messages and then flush it once
            // the amount of messages achieved some kind of value (that way
            // making file logging throttled), but  in this case I have no idea
            // how to make a kind of singleton-ish storage of log messages (for
            // different service requirements) that is able to be flushed at the
            // end of the application life cycle
            //
            // imho a way of locking using ReaderWriterLockSlim is more descriptive
            // way of handling this exceptional situations, and (not relevant here,
            // but nevertheless) this is a much better practise in the cases where
            // there's a reader that exists, and this class will allow to read
            // while writing occurs. but I just think that using lock statement
            // in this case is more optimal than calling a method
            
            // _readWriteLock.EnterWriteLock();
            try
            {
                lock(typeof(DateFileLogger)){
                    using (StreamWriter fileWriter = new StreamWriter(_logFilePath, true))
                    {
                        fileWriter.WriteLine($"[ {logTime} ][ {logLevel} ]");
                        fileWriter.WriteLine(logContent);
                        fileWriter.WriteLine();
                        fileWriter.Flush();
                    }
                }
            }
            catch
            {
                // ignored
            }
            // finally
            // {
                // _readWriteLock.ExitWriteLock();
            // }

        }
    }
}