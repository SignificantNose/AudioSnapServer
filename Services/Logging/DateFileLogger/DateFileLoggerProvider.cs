using AudioSnapServer.Options;
using Microsoft.Extensions.Options;

namespace AudioSnapServer.Services.DateFileLogger;

[ProviderAlias("DateFile")]
public class DateFileLoggerProvider : ILoggerProvider
{
    private string _logFileName;
    private string _logDirPath;
    // private IOptions<DateFileLoggerOptions> _loggerOptions;

    public DateFileLoggerProvider(
        // IOptions<DateFileLoggerOptions> loggerOptions,
        string logDirPath,
        string logFileName)
    {
        // _loggerOptions = loggerOptions;
        _logDirPath = logDirPath;
        _logFileName = logFileName;
    }

    public void Dispose() { }

    public ILogger CreateLogger(string categoryName)
    {
        // _logDirPath = _loggerOptions.Value.LogDirPath;
        return new DateFileLogger(_logDirPath, _logFileName);
    }
}