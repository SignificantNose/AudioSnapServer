using System.Net;

namespace AudioSnapServer.Services.Logging;

public class CompactHttpLoggingDelegateHandler : DelegatingHandler
{
    private readonly ILogger _logger;

    public CompactHttpLoggingDelegateHandler(ILogger logger)
    {
        // don't know if it's possible to pass a null
        // parameter in this context, but it's safer 
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        HttpResponseMessage response;
        
        using (Log.BeginRequestPipelineScope(_logger, request))
        {
            // TODO: make this HttpLogger special for the API communication services
            // (thought: make response IDs in log, so the response can be
            // viewed in a database?)
            
            Log.RequestPipelineStart(_logger, request);
            
            DateTime start = DateTime.Now;
            response = await base.SendAsync(request, cancellationToken);
            DateTime end = DateTime.Now;
            TimeSpan deltaTime = end - start;
            
            Log.RequestPipelineEnd(_logger, deltaTime.TotalMilliseconds, response);
        }

        return response;
    }


    private static class Log
    {
        // private static readonly int MAX_URI_LENGTH = 40;
        
        private static class EventIDs
        {
            public static readonly EventId PipelineStart = new EventId(100, "RequestPipelineStart");
            public static readonly EventId PipelineEnd = new EventId(101, "RequestPipelineEnd");
        }

        private static readonly Func<ILogger, HttpMethod, Uri, IDisposable> _beginRequestPipelineScope =
            LoggerMessage.DefineScope<HttpMethod, Uri>(
                "HTTP {HttpMethod} {RequestUri}");

        private static readonly Action<ILogger, HttpMethod, Uri, Exception> _requestPipelineStart =
            LoggerMessage.Define<HttpMethod, Uri>(
                LogLevel.Information,
                EventIDs.PipelineStart,
                "Start processing HTTP request {HttpMethod} {RequestUri}");

        private static readonly Action<ILogger, HttpStatusCode, double, Exception> _requestPipelineEnd =
            LoggerMessage.Define<HttpStatusCode, double>(
                LogLevel.Information,
                EventIDs.PipelineEnd,
                "End processing HTTP request - {StatusCode} (t = {time}ms)");
        
        public static IDisposable BeginRequestPipelineScope(ILogger logger, HttpRequestMessage request)
        {
            // string UriDisplay = request.RequestUri.ToString();
            // if (UriDisplay.Length >= MAX_URI_LENGTH)
            // {
                // UriDisplay = UriDisplay.Substring(0, MAX_URI_LENGTH - 3) + "...";
            // }

            return _beginRequestPipelineScope(logger, request.Method, request.RequestUri);
        }

        public static void RequestPipelineStart(ILogger logger, HttpRequestMessage request)
        {
            _requestPipelineStart(logger, request.Method, request.RequestUri, null);
        }

        public static void RequestPipelineEnd(ILogger logger, double responseMs, HttpResponseMessage response)
        {
            _requestPipelineEnd(logger, response.StatusCode, responseMs, null);
        }
        
    }
}
