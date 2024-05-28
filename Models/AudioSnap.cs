namespace AudioSnapServer.Models;


public class AudioSnap
{
    // Lord forgive me, for I have made such monstrosity of a method
    // (a pretty good method (as for my eyes) is described here:
    // https://www.codeproject.com/Tips/824912/Mapping-Properties-to-String-Keys
    // but nested properties must be processed with dots in the names of the properties.
    // Path to a property could've been acquired from an expression, but there are
    // methods that can be called on a property (e.x. list item indexation),
    // and that will not work on a simple GetProperty method, therefore, a more
    // complex mapping logic is needed. Moreover, some json properties must be
    // computed from a set of nested properties, which is not possible with such
    // approach. Therefore, a decision has been made to make a dictionary of json
    // fields that point to functions that return objects.
    
    public static readonly Dictionary<string, Func<AudioSnap, object>> PropertyMappings = 
        new Dictionary<string, Func<AudioSnap, object>>()
    {
        {"album", snap => snap.RecordingPrioritizedRelease.ReleaseGroup.Title },   
        
        // make some kind of expression that takes all Name values
        // from artist field of all the artist credits??
        {"album-artists", snap => snap.RecordingPrioritizedRelease.ReleaseGroup.ArtistCredits.Select(ac => ac.Artist.Name) },   
        {"album-artists-sort",snap => snap.RecordingPrioritizedRelease.ReleaseGroup.ArtistCredits.Select(ac => ac.Artist.SortName)},   
        {"artists", snap => snap.ChosenTrack.ArtistCredits.Select(ac => ac.Artist.Name) },
        {"disc", snap => snap.Disc},
        {"disc-count",snap => snap.DiscCount},
        {"genres", snap => snap.ChosenTrack.Genres},
        {"isrc",snap => string.Join("; ",snap.RecordingResponse.ISRCs)},
        {"length", snap => snap.RecordingResponse.LengthMs},
        {"music-brainz-artist-id", snap => string.Join("; ", snap.ChosenTrack.ArtistCredits.Select(ac => ac.Artist.Id))},
        {"music-brainz-disc-id", snap => string.Empty},
        {"music-brainz-release-id", snap => snap.RecordingPrioritizedRelease.Id},
        {"music-brainz-release-status", snap => snap.RecordingPrioritizedRelease.Status},
        {"music-brainz-track-id", snap => snap.ChosenTrack.Id},
        {"track", snap => snap.ReleaseMedia.TrackOffset+1},
        {"track-count", snap => snap.ReleaseMedia.TrackCount},
        {"title", snap => snap.RecordingResponse.Title},
        {"year", snap => DateTime.Parse(snap.RecordingPrioritizedRelease.ReleaseGroup.FirstReleaseDate).Year}
    };
    public int Disc { get; set; } = 1;
    public int DiscCount { get; set; } = 1;
    public string MusicBrainzDiscId = "";

    public AcoustID_APIResponse AcoustIDResponse;

    public CoverArtArchive_APIResponse CoverArtArchiveResponse;
    
    // isrcs, length, title
    public MusicBrainz_APIResponse RecordingResponse;
    
    // Release chosen based on a set of priorities
    //
    // album, album-artists, album-artists-sort, year,
    // music-brainz-release-id, music-brainz-release-status
    public MusicBrainz_APIResponse.Release RecordingPrioritizedRelease;

    
    // A Media element from a prioritized release. Defaults to
    // the first element of media (if it exists at all)
    //
    // track-1, track-count
    public MusicBrainz_APIResponse.Media ReleaseMedia;
    
    // Track acquired from list of the Release response
    // based on an offset
    //
    // artists, genres, track-id, music-brainz-artist-id
    public MusicBrainz_APIResponse.Track ChosenTrack;
    
    public MusicBrainz_APIResponse ReleaseResponse;
}