using Microsoft.Extensions.Http;

namespace AudioSnapServer.Services.Logging;

public class CompactHttpLoggingFilter : IHttpMessageHandlerBuilderFilter
{
    private ILoggerFactory _loggerFactory;
    
    public CompactHttpLoggingFilter(ILoggerFactory loggerFactory)
    {
        if (loggerFactory == null) throw new ArgumentNullException(nameof(loggerFactory));
        _loggerFactory = loggerFactory;
    }

    public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next)
    {
        if (next == null) throw new ArgumentNullException(nameof(next));
        
        return (builder) =>
        {
            next(builder);

            string loggerName = string.IsNullOrEmpty(builder.Name) ? "Default" : builder.Name;
            ILogger outerLogger = _loggerFactory.CreateLogger($"System.Net.Http.HttpClient.{loggerName}.LogicalHandler");

            builder.AdditionalHandlers.Insert(0,new CompactHttpLoggingDelegateHandler(outerLogger));
        };
    }
}