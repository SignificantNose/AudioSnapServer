using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AudioSnapServer.Models.ResponseStorage;

public class AcoustIDStorage
{
    // https://learn.microsoft.com/en-us/ef/core/modeling/keyless-entity-types?tabs=data-annotations
    // says that keyless entity types are never tracked for
    // changes in the DbContext => never inserted, updated
    // or deleted on the database. Not something I'm looking
    // for, so I will define a separate field as an ID
    
    // also, hoping that it is an auto-incremented property
    // in this case that must not be provided (acc. to Primary
    // keys section in:
    // https://learn.microsoft.com/en-us/ef/core/modeling/generated-properties?tabs=data-annotations
    // )
    // upd: primary key is changed from int to string
    [Key]
    [Column("acoustid")]
    public string AcoustID { get; set; }

    [Column("hash")]
    public uint Hash { get; set; }
    
    [Column("duration")]
    public long Duration { get; set; }
    
    // use most-scored recordingID from the response
    [Column("score")]
    public double MatchingScore { get; set; }
    
    [Column("recording_id")]
    public string RecordingID { get; set; }
}