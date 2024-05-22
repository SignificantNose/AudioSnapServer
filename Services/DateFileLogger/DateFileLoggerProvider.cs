namespace AudioSnapServer.Services.DateFileLogger;

[ProviderAlias("DateFile")]
public class DateFileLoggerProvider : ILoggerProvider
{
    private string _logFileName;
    private string _logDirPath;
    // private IOptions<DateFileLoggerConfiguration> _config;

    // public DateFileLoggerProvider(
    //     IOptionsMonitor<DateFileLoggerConfiguration> config, string logFileName)
    // {
    //     _logFileName = logFileName;
    //     _logDirPath = config.CurrentValue.Path;
    // }

    public DateFileLoggerProvider(
        // IOptions<MyFileLoggerConfiguration> config,
        string logDirPath,
        string logFileName)
    {
        // _config = config;
        _logDirPath = logDirPath;
        _logFileName = logFileName;
    }

    public void Dispose() { }

    public ILogger CreateLogger(string categoryName)
    {
        // _logDirPath = _config.Value.LogDirPath;
        return new DateFileLogger(_logDirPath, _logFileName);
    }
}