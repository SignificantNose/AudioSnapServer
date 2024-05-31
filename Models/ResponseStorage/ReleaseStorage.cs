using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AudioSnapServer.Models.ResponseStorage;

public class ReleaseStorage
{
    [Key]
    [Column("release_id")]
    public string ReleaseID { get; set; }
 
    // json
    [Column("release_response")]
    public string? ReleaseResponse { get; set; }
 
    // json
    [Column("cover-response")]
    public string? CoverResponse { get; set; }
}

public record class ReleaseDBResponse(
    string ReleaseID,
    string? ReleaseJson
    );
    
public record class CoverArtDBResponse(
    string ReleaseID,
    string? CoverArtJson
    );