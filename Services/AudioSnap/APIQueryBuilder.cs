namespace AudioSnapServer.Services.AudioSnap;

/// <summary>
/// Class for building query strings to access APIs
/// </summary>
public static class APIQueryBuilder
{
    public enum MusicBrainzEntry
    {
        ERecording,
        ERelease
    };

    /// <summary>
    /// Helper class for making the correspondence between the entry type
    /// and its name and default query parameters (for MusicBrainz API service)
    /// </summary>
    private record class MusicBrainzQuery(
        string Entry, string Parameters);

    private static readonly Dictionary<MusicBrainzEntry, MusicBrainzQuery> QueryParameters =
        new()
        {
            { MusicBrainzEntry.ERecording, new MusicBrainzQuery("recording", "artist-credits+isrcs+releases+url-rels+genres+release-groups+discids") },
            { MusicBrainzEntry.ERelease, new MusicBrainzQuery("release", "artist-credits+labels+discids+recordings+url-rels") }
        };
    
    
    /// <summary>
    /// Form a query string to AcoustID API that expects to receive only recording IDs
    /// </summary>
    public static string Q_AcoustID(string ClientKey, int duration, string fingerprint)
    {
        return $"lookup?client={ClientKey}&duration={duration}&fingerprint={fingerprint}&meta=recordingids&format=json";
    }
    
    /// <summary>
    /// Form a query string to MusicBrainz API service for a
    /// specific entry with default parameters
    /// </summary>
    public static string Q_MusicBrainz(MusicBrainzEntry entry, string entryID)
    {
        MusicBrainzQuery queryParams = QueryParameters[entry];
        return Q_FillMusicBrainzQuery(queryParams.Entry, entryID, queryParams.Parameters);
    }

    /// <summary>
    /// Acquire Cover Art information for a release from CoverArtArchive API service
    /// </summary>
    public static string Q_CoverArtArchive(string releaseID)
    {
        return $"release/{releaseID}";
    }


    /// <summary>
    /// Form a query string to MusicBrainz API service for a specific entry
    /// </summary>
    private static string Q_FillMusicBrainzQuery(string entry, string entryID, string queryParams)
    {
        return $"{entry}/{entryID}?inc={queryParams}&fmt=json";
    }
}