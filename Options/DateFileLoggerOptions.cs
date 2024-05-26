namespace AudioSnapServer.Options;

public sealed class DateFileLoggerOptions
{
    public static readonly string ConfigurationSectionName = "Logging:DateFile";
    
    public required string LogDirPath { get; set; } = "logs/";
}