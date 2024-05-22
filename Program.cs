using AudioSnapServer.Services.AudioSnap;
using AudioSnapServer.Services.DateFileLogger;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddControllers();
builder.Services.AddScoped<IAudioSnapService, AudioSnapService>();

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
// IOptions implementation kinda doesn't work, so: 
builder.Logging.AddDateFile(builder.Configuration["Logging:DateFile:LogDirPath"]);

var app = builder.Build();

app.MapGet("/", () =>
{
    // not as critical, but with the purpose
    // of seeing how it will display
    app.Logger.LogCritical("OMG AN ENDPOINT HAS BEEN ACCESSED!!!");
    return "Hello World!";
});

app.MapControllers();

app.Run();