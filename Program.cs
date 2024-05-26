using System.ComponentModel.DataAnnotations;
using System.Net.Http.Headers;
using AudioSnapServer.Options;
using AudioSnapServer.Services.AudioSnap;
using AudioSnapServer.Services.DateFileLogger;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();

// Utilizing AppOptionsExtensions initialization methods
// builder.Services.AddLoggingOptions(builder.Configuration);       // using Options is redundant here imho
builder.Services.AddAPIOptions(builder.Configuration);
builder.Services.AddAPIHttpClients();

// Initializing loggers
string? logDirPath = builder.Configuration["Logging:DateFile:LogDirPath"];
if (logDirPath == null)
{
    logDirPath = "logs/";
}
if (!Directory.Exists(logDirPath))
{
    Directory.CreateDirectory(logDirPath);
}
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDateFile(logDirPath);


builder.Services.AddScoped<IAudioSnapService, AudioSnapService>();


var app = builder.Build();

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

app.MapControllers();

app.Run();