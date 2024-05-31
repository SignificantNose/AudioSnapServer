using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AudioSnapServer.Models.ResponseStorage;

public class RecordingStorage
{
    [Key]
    [Column("recording_id")]
    public string RecordingID { get; set; }
    
    // json
    [Column("recording_response")]
    public string RecordingResponse { get; set; }
}