using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace AudioSnapServer.Options;

public sealed class ExternalAPIClientOptions
{
    public static readonly  string ConfigurationSectionName = "ExternalAPI-Client";

    [Required(ErrorMessage = "AcoustID API key must be provided in the configuration.")]
    public required string AcoustIDKey { get; set; }

    [Required(ErrorMessage = "UserAgent header must be provided in the configuration" +
                             "(see https://musicbrainz.org/doc/MusicBrainz_API/Rate_Limiting#Provide_meaningful_User-Agent_strings)")]
    public required string UserAgent { get; set; }
    
    [Required(ErrorMessage="Application version must be provided in the configuration" +
                           "(see https://musicbrainz.org/doc/MusicBrainz_API/Rate_Limiting#Provide_meaningful_User-Agent_strings)")]
    public required string Version { get; set; }
    
    [Required(ErrorMessage = "Contact Email must be provided in the configuration" +
                             "(see https://musicbrainz.org/doc/MusicBrainz_API/Rate_Limiting#Provide_meaningful_User-Agent_strings)")]
    public required string ContactEmail { get; set; }
}