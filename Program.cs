using System.ComponentModel.DataAnnotations;
using System.Net.Http.Headers;
using System.Threading.RateLimiting;
using AudioSnapServer.Data;
using AudioSnapServer.Models.ResponseStorage;
using AudioSnapServer.Options;
using AudioSnapServer.Services.AudioSnap;
using AudioSnapServer.Services.DateFileLogger;
using AudioSnapServer.Services.Logging;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();

SnapRateLimitOptions rateLimitingOptions = new SnapRateLimitOptions();
builder.Configuration.GetSection(SnapRateLimitOptions.ConfigurationSectionName).Bind(rateLimitingOptions);
string fixedPolicy = "fixed";

builder.Services.AddRateLimiter(_ => _.AddFixedWindowLimiter(
    policyName: fixedPolicy, options =>
    {
        options.PermitLimit = rateLimitingOptions.PermitLimit;
        options.Window = TimeSpan.FromSeconds(rateLimitingOptions.Window);
        options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    }
));

string connString = builder.Configuration.GetConnectionString("AppDbConnectionString");
builder.Services.AddDbContext<AudioSnapDbContext>(options =>
{
    options.UseMySql(connString, ServerVersion.AutoDetect(connString));
});

// Utilizing AppOptionsExtensions initialization methods
builder.Services.AddAPIOptions(builder.Configuration);
builder.Services.AddAPIHttpClients();

// Initializing loggers
// (imho using Options pattern is redundant
// in the context of logger initialization)
string? logDirPath = builder.Configuration["Logging:DateFile:LogDirPath"];
if (logDirPath == null)
{
    logDirPath = "logs"+Path.DirectorySeparatorChar;
}
if (!Directory.Exists(logDirPath))
{
    Directory.CreateDirectory(logDirPath);
}
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDateFile(logDirPath);


builder.Services.AddScoped<IAudioSnapService, AudioSnapService>();

builder.Services.Replace(ServiceDescriptor.Singleton<IHttpMessageHandlerBuilderFilter, CompactHttpLoggingFilter>());


var app = builder.Build();
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error-development");
}
else
{
    app.UseExceptionHandler("/error");
}


app.MapGet("/", () =>
{
    // not as critical, but with the purpose
    // of seeing how it will display
    app.Logger.LogCritical("OMG AN ENDPOINT HAS BEEN ACCESSED!!!");
    return "Hello World!";
});

app.MapGet("/testdb", (AudioSnapDbContext ctx) =>
{
    ctx.AcoustIDs.Add(new AcoustIDStorage()
    {
        Hash = int.MaxValue,
        Duration = 100,
        MatchingScore = 0.15,
        RecordingID = "124-4111"
    });
    ctx.SaveChanges();
    
    List<AcoustIDStorage> storages = new List<AcoustIDStorage>();
    try
    {
        storages = ctx.AcoustIDs.ToList();
    }
    catch
    {
        // logger.LogWarning($"Database access error: {ex.Message}");   
        // throw;
    }

    string result = "";
    foreach (var entry in storages)
    {
        result += entry.Hash;
    }

    return Task.FromResult(result);
});

app.MapControllers();

app.Run();