namespace AudioSnapServer.Options;

public class SnapRateLimitOptions
{
    public static readonly string ConfigurationSectionName = "SnapRateLimit";
    
    // In case some other properties will be required, 
    // they are not listed here, as their default values
    // are not yet known to fit other FixedWindow rate 
    // limiters int the API HTTP clients
    public int PermitLimit { get; set; } = 5;
    public int Window { get; set; } = 1;        // in seconds
}