using System.Text.Json.Serialization;
using AudioSnapServer.Models.ResponseStorage;

namespace AudioSnapServer.Models;

/// <summary>
/// Issue: this structure might not be present in the response, as
/// there can be a request with (for ex.) only releaseids meta, and
/// there won't be any Recording array included, only Release array.
/// It can be avoided to include Release into AcoustID_Result as a
/// possible response, but IMHO it's not as flexible, as there might
/// be other cases where there might be a similar error. So a decision
/// has been made to include all the data that is available in a
/// request with all meta included (also because it satisfies the
/// requirements to the request where releaseid is fetched)
///
/// Upd: new structure. much more compact. include only the parameters
/// needed for theserver to make another query. we only need the releaseids
/// </summary>
/// <param name="Results"></param>
/// <param name="Status"></param>
public record class AcoustID_APIResponse(
    [property: JsonPropertyName("results")] List<AcoustID_APIResponse.Result> Results,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("error")] AcoustID_APIResponse.Error QueryError
)
{
    public static AcoustID_APIResponse FormFromDbResponse(AcoustIDStorage entry)
    {
        return new AcoustID_APIResponse(
            Results: new List<AcoustID_APIResponse.Result>()
            {
                new AcoustID_APIResponse.Result(
                    entry.AcoustID,
                    entry.MatchingScore,
                    new List<AcoustID_APIResponse.Recording>()
                    {
                        new AcoustID_APIResponse.Recording(entry.RecordingID)
                    }
                )
            },
            Status: "ok",
            QueryError: null
        );
    }

    public record class Result(
        [property: JsonPropertyName("id")] string AcoustID_ID,
        [property: JsonPropertyName("score")] double MatchScore,
        [property: JsonPropertyName("recordings")] List<Recording> Recordings
    );

    public record class Recording(
        [property: JsonPropertyName("id")] string RecordingID 
    );

    public record class Error(
        [property: JsonPropertyName("code")] int Code,
        [property: JsonPropertyName("message")] string Message
    );
}

// public record class AcoustID_Result(
    // [property: JsonPropertyName("id")] string AcoustID_ID,
    // [property: JsonPropertyName("score")] double MatchScore,
    // [property: JsonPropertyName("recordings")] List<AcoustID_Recording> Recordings
// );
//
// public record class AcoustID_Recording(
    // [property: JsonPropertyName("id")] string RecordingID 
// );
//
// public record class AcoustID_Error(
    // [property: JsonPropertyName("code")] int Code,
    // [property: JsonPropertyName("message")] string Message
// );
//
// public record class AID_ReleaseGroup(
//     [property: JsonPropertyName("id")] string ReleaseGroupID,
//     [property: JsonPropertyName("releases")] List<AID_Release> Releases
//     );
//
// public record class AID_Release(
//     [property: JsonPropertyName("id")] string ReleaseID,
//     [property: JsonPropertyName("mediums")] List<AID_Medium> Mediums
//     );
//
// public record class AID_Medium(
//     [property: JsonPropertyName("format")] string Format,
//     [property: JsonPropertyName("position")] int Position,
//     [property: JsonPropertyName("track_count")] int TrackCount,
//     [property: JsonPropertyName("tracks")] List<AID_Track> Tracks
//     );
//
// public record class AID_Track(
//     [property: JsonPropertyName("id")] string TrackID,
//     [property: JsonPropertyName("position")] int Position,
//     [property: JsonPropertyName("title")] string Title,
//     [property: JsonPropertyName("artists")] List<AID_Artist> Artists
//     );
//
// public record class AID_Artist(
//     [property: JsonPropertyName("id")] string ArtistID,
//     [property: JsonPropertyName("name")] string Name
//     );
