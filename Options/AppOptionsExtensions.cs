using System.Net.Http.Headers;
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
        
        // AcoustID HttpClient
        services.AddHttpClient("acoustid", (serviceProvider,httpClient) =>
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
    
        }).SetHandlerLifetime(TimeSpan.FromMinutes(5));

        // MusicBrainz HttpClient
        services.AddHttpClient("musicbrainz", (serviceProvider,httpClient) =>
        {
            ExternalAPIClientOptions clientOptions =
                serviceProvider.GetRequiredService<IOptions<ExternalAPIClientOptions>>().Value;
            ExternalAPIHostsOptions hostsOptions =
                serviceProvider.GetRequiredService<IOptions<ExternalAPIHostsOptions>>().Value;
    
            httpClient.DefaultRequestHeaders.Accept.Clear();
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            httpClient.DefaultRequestHeaders.Add("User-Agent",$"{clientOptions.UserAgent}/{clientOptions.Version} ( {clientOptions.ContactEmail} )");
    
            httpClient.BaseAddress = hostsOptions.MusicBrainz;
    
        }).SetHandlerLifetime(TimeSpan.FromMinutes(5));

        // CoverArtArchive HttpClient
        services.AddHttpClient("coverartarchive", (serviceProvider,httpClient) =>
        {
            ExternalAPIHostsOptions hostsOptions =
                serviceProvider.GetRequiredService<IOptions<ExternalAPIHostsOptions>>().Value;
    
            httpClient.BaseAddress = hostsOptions.CoverArtArchive;
        }).SetHandlerLifetime(TimeSpan.FromMinutes(5));

        return services;
    }
}