using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Configuration;

namespace AudioSnapServer.Services.DateFileLogger;

public static class DateFileLoggerExtensions
{
    public static ILoggingBuilder AddDateFile(
        this ILoggingBuilder builder, 
        string logDirPath, 
        string? logFileName = null)
    {
        if (logFileName == null)
        {
            logFileName = $"{DateTime.Now:s}.log".Replace("T", "   ").Replace(":","_");
        }
        // builder.AddConfiguration();
        // builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, DateFileLoggerProvider>());
        // LoggerProviderOptions.RegisterProviderOptions<DateFileLoggerConfiguration, DateFileLoggerProvider>(builder.Services);
        builder.AddProvider(new DateFileLoggerProvider(logDirPath, logFileName));
        return builder;

    }
}