namespace AudioSnapServer.Services.AudioSnap;

public static class APIQueryBuilder
{
    /// <summary>
    /// Form a query string to AcoustID API that expects to receive only recording IDs
    /// </summary>
    public static string Q_AcoustID(string ClientKey, int duration, string fingerprint)
    {
        return $"lookup?client={ClientKey}&duration={duration}&fingerprint={fingerprint}&meta=recordingids&format=json";
    }
    
    /// <summary>
    /// Form a query string to MusicBrainz API service for a specific entry
    /// </summary>
    public static string Q_MusicBrainz(string entry, string entryID, string queryParams)
    {
        return $"{entry}/{entryID}?inc={queryParams}&fmt=json";
    }

    /// <summary>
    /// Acquire Cover Art information for a release from CoverArtArchive API service
    /// </summary>
    public static string Q_CoverArtArchive(string releaseID)
    {
        return $"release/{releaseID}";
    }
}