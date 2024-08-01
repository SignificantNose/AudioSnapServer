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
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// foreach(var x in builder.Configuration.GetSection("Logging").GetChildren()){
//     Console.WriteLine(x.Key);
// }


string? fileLogDirPath = builder.Configuration["Logging:DateFile:LogDirPath"];
string? fileLogStatus = null;
// kinda redundant
bool fileLogEnabled = fileLogDirPath != null;
bool fileLogSuccessful = true;
if (fileLogEnabled)
{
    if (!Directory.Exists(fileLogDirPath))
    {
        try
        {
            Directory.CreateDirectory(fileLogDirPath);
        }
        catch (Exception ex)
        {
            fileLogSuccessful = false;
            // logger.LogError(ex, "Error while specifying the file logging path");
            fileLogStatus = "Error while specifying the file logging path";
        }
    }
    if(fileLogSuccessful){
        builder.Logging.AddDateFile(fileLogDirPath);
        // logger.LogInformation($"File logging enabled at path: {logDirPath}");
        fileLogStatus = $"File logging enabled at path: {fileLogDirPath}";
    }
}




builder.Services.AddScoped<IAudioSnapService, AudioSnapService>();

builder.Services.Replace(ServiceDescriptor.Singleton<IHttpMessageHandlerBuilderFilter, CompactHttpLoggingFilter>());

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    
    // DB initialization
    try
    {
        var context = services.GetRequiredService<AudioSnapDbContext>();
        var created = context.Database.EnsureCreated();
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Database creation failed");
        // TODO: make the logic for turning off the database interaction here
    }


    // file logging status
    if(fileLogEnabled){
        if(fileLogSuccessful){
            logger.LogInformation(fileLogStatus);
        }
        else{
            logger.LogError(fileLogStatus);
        }
    }
}

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