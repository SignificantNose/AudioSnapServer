using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace AudioSnapServer.Options;

public sealed class ExternalAPIHostsOptions
{
    public static readonly  string ConfigurationSectionName = "ExternalAPI-Hosts";
    
    // Default values are provided in case the values
    // are not present in the configuration (the values
    // can be provided in case the URI to API hosts change)

    [Required] 
    public required Uri AcoustID { get; set; } = new Uri("https://api.acoustid.org/v2/");
    
    [Required]
    public required Uri MusicBrainz { get; set; } = new Uri("https://musicbrainz.org/ws/2/");

    [Required]
    public required Uri CoverArtArchive { get; set; } = new Uri("https://coverartarchive.org/");
}