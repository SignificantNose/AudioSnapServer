using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;

namespace AudioSnapServer.Options;

public static class AppOptionsExtensions
{
    public static IServiceCollection AddAPIOptions(
        this IServiceCollection services,
        ConfigurationManager config)
    {
        services
            .AddOptionsWithValidateOnStart<ExternalAPIClientOptions>()
            .Bind(config.GetSection(ExternalAPIClientOptions.ConfigurationSectionName))
            .ValidateDataAnnotations();
        
        // just with the purpose of making a validation
        // of the presence of base addresses. the options
        // will not be used anywhere else.
        // upd: maybe it might be useful somewhere else?..
        services.Configure<ExternalAPIHostsOptions>(
            config.GetSection(ExternalAPIHostsOptions.ConfigurationSectionName));
        
        return services;
    }

    public static IServiceCollection AddAPIHttpClients(
        this IServiceCollection services)
    {
        // Maybe I could've made this:
        //services.BuildServiceProvider();
        // and use the same ServiceProvider in each client,
        // but something tells me there might be something 
        // wrong (scopes-term)
        
        // TODO: issue: I wanted to implement a rate limiter for http clients,
        // but "the handler cannot be reused", an exception says. Some kind of
        // static instance is required here that is addressed by all the handlers 
        // of AcoustID http handler. Same goes for MusicBrainz. For now, a rate 
        // limit on /snap POST request is set.
        
        // AcoustID HttpClient
        services
            .AddHttpClient("acoustid", (serviceProvider,httpClient) =>
        {
            ExternalAPIClientOptions clientOptions =
                serviceProvider.GetRequiredService<IOptions<ExternalAPIClientOptions>>().Value;
            ExternalAPIHostsOptions hostsOptions =
                serviceProvider.GetRequiredService<IOptions<ExternalAPIHostsOptions>>().Value;
    
            // AcoustID doesn't need to know this, but we'll be a
            // good user and provide these headers anyway
            httpClient.DefaultRequestHeaders.Accept.Clear();
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            httpClient.DefaultRequestHeaders.Add("User-Agent",$"{clientOptions.UserAgent}/{clientOptions.Version} ( {clientOptions.ContactEmail} )");
    
            httpClient.BaseAddress = hostsOptions.AcoustID;
        })
            // .AddHttpMessageHandler(()=> new HttpAPIRateLimiter(
            //     new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
            //     {   
            //         PermitLimit = 3,
            //         Window = TimeSpan.FromSeconds(1),
            //         QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            //     })))
            .SetHandlerLifetime(TimeSpan.FromMinutes(5));

        // MusicBrainz HttpClient
        services
            .AddHttpClient("musicbrainz", (serviceProvider,httpClient) =>
        {
            ExternalAPIClientOptions clientOptions =
                serviceProvider.GetRequiredService<IOptions<ExternalAPIClientOptions>>().Value;
            ExternalAPIHostsOptions hostsOptions =
                serviceProvider.GetRequiredService<IOptions<ExternalAPIHostsOptions>>().Value;
    
            httpClient.DefaultRequestHeaders.Accept.Clear();
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            httpClient.DefaultRequestHeaders.Add("User-Agent",$"{clientOptions.UserAgent}/{clientOptions.Version} ( {clientOptions.ContactEmail} )");
    
            httpClient.BaseAddress = hostsOptions.MusicBrainz;
    
        })
            // .AddHttpMessageHandler(()=> new HttpAPIRateLimiter(
            //     new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
            //     {   
            //         PermitLimit = 4,
            //         Window = TimeSpan.FromSeconds(1),
            //         QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            //     })))
            .SetHandlerLifetime(TimeSpan.FromMinutes(5));

        // CoverArtArchive HttpClient
        services
            .AddHttpClient("coverartarchive", (serviceProvider,httpClient) =>
        {
            ExternalAPIHostsOptions hostsOptions =
                serviceProvider.GetRequiredService<IOptions<ExternalAPIHostsOptions>>().Value;
    
            httpClient.BaseAddress = hostsOptions.CoverArtArchive;
            httpClient.Timeout = TimeSpan.FromSeconds(5);
        })
            .SetHandlerLifetime(TimeSpan.FromMinutes(5));

        return services;
    }
}

// public class HttpAPIRateLimiter : DelegatingHandler
// {
//     private readonly List<DateTimeOffset> _callLog =
//         new List<DateTimeOffset>();
//     private readonly TimeSpan _limitTime;
//     private readonly int _limitCount;
//
//     public HttpAPIRateLimiter(int limitCount, TimeSpan limitTime)
//     {
//         _limitCount = limitCount;
//         _limitTime = limitTime;
//     }
//
//     protected override async Task<HttpResponseMessage> SendAsync(
//         HttpRequestMessage request,
//         CancellationToken cancellationToken)
//     {
//         var now = DateTimeOffset.UtcNow;
//
//         lock (_callLog)
//         {
//             _callLog.Add(now);
//
//             while (_callLog.Count > _limitCount)
//                 _callLog.RemoveAt(0);
//         }
//
//         await LimitDelay(now);
//
//         return await base.SendAsync(request, cancellationToken);
//     }
//
//     private async Task LimitDelay(DateTimeOffset now)
//     {
//         if (_callLog.Count < _limitCount)
//             return;
//
//         var limit = now.Add(-_limitTime);
//
//         var lastCall = DateTimeOffset.MinValue;
//         var shouldLock = false;
//
//         lock (_callLog)
//         {
//             lastCall = _callLog.FirstOrDefault();
//             shouldLock = _callLog.Count(x => x >= limit) >= _limitCount;
//         }
//
//         var delayTime = shouldLock && (lastCall > DateTimeOffset.MinValue)
//             ? (limit - lastCall)
//             : TimeSpan.Zero;
//
//         if (delayTime > TimeSpan.Zero)
//             await Task.Delay(delayTime);
//     }
// }


public class HttpAPIRateLimiter (RateLimiter limiter) : DelegatingHandler(new HttpClientHandler()), IAsyncDisposable
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using RateLimitLease lease = await limiter.AcquireAsync(permitCount: 1, cancellationToken);

        if (lease.IsAcquired)
        {
            return await base.SendAsync(request, cancellationToken);
        }

        // else, apply the retry-after policy

        HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        if (lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter))
        {
            response.Headers.Add(
                "Retry-After",
                ((int)retryAfter.TotalSeconds).ToString(NumberFormatInfo.InvariantInfo));
        }

        return response;
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        await limiter.DisposeAsync().ConfigureAwait(false);
        
        Dispose(disposing:false);
        GC.SuppressFinalize(this);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            limiter.Dispose();
        }
    }
}