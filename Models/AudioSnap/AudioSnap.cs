namespace AudioSnapServer.Models;


public class AudioSnap
{
    public static readonly byte NC_AID_RESPONSE = 0b00000001;
    public static readonly byte NC_MB_RECORDINGID = 0b00000010;
    
    public static readonly byte NC_MB_RECORDINGRESPONSE = 0b00000100;
    public static readonly byte NC_MB_RECPRIORITIZEDRELEASE = 0b00001000;
    public static readonly byte NC_MB_RELEASEMEDIA = 0b00010000;
    
    public static readonly byte NC_MB_RELEASERESPONSE = 0b00100000;
    public static readonly byte NC_MB_CHOSENTRACK = 0b01000000;
    
    public static readonly byte NC_CAA_RESPONSE = 0b10000000;

    // for properties with default values:
    public static readonly byte NC_SPECIALPRESENT = 0b11111111;
    
    
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

    public record class Mapping(
        Func<AudioSnap, object> GetMappedValue,
        byte Mask
    );
    
    public static readonly Dictionary<string, Mapping> PropertyMappings = 
        new()
        {
        {"album", new Mapping(snap => snap.RecordingPrioritizedRelease.ReleaseGroup.Title, NC_MB_RECPRIORITIZEDRELEASE ) },   
        {"album-artists", new Mapping(snap => snap.RecordingPrioritizedRelease.ReleaseGroup.ArtistCredits.Select(ac => ac.Artist.Name), NC_MB_RECPRIORITIZEDRELEASE ) },   
        {"album-artists-sort",new Mapping(snap => snap.RecordingPrioritizedRelease.ReleaseGroup.ArtistCredits.Select(ac => ac.Artist.SortName),NC_MB_RECPRIORITIZEDRELEASE )},   
        {"artists", new Mapping(snap => snap.ChosenTrack.ArtistCredits.Select(ac => ac.Artist.Name), NC_MB_CHOSENTRACK) },
        {"disc", new Mapping(snap => snap.Disc,NC_SPECIALPRESENT)},
        {"disc-count",new Mapping(snap => snap.DiscCount,NC_SPECIALPRESENT)},
        {"genres", new Mapping(snap => snap.ChosenTrack.Genres,NC_MB_CHOSENTRACK)},
        {"isrcs",new Mapping(snap => string.Join("; ",snap.RecordingResponse.ISRCs),NC_MB_RECORDINGRESPONSE)},
        {"length", new Mapping(snap => snap.RecordingResponse.LengthMs,NC_MB_RECORDINGRESPONSE)},
        {"acoustid", new Mapping(snap => snap.AcoustIDResponse.Results[0].AcoustID_ID,NC_AID_RESPONSE)},
        {"music-brainz-artist-id", new Mapping(snap => string.Join("; ", snap.ChosenTrack.ArtistCredits.Select(ac => ac.Artist.Id)),NC_MB_CHOSENTRACK)},
        {"music-brainz-disc-id", new Mapping(snap => snap.MusicBrainzDiscID, NC_SPECIALPRESENT)},
        {"music-brainz-recording-id", new Mapping(snap => snap.RecordingID,NC_MB_RECORDINGID)},
        {"music-brainz-release-id", new Mapping(snap => snap.RecordingPrioritizedRelease.Id,NC_MB_RECPRIORITIZEDRELEASE)},
        {"music-brainz-release-status", new Mapping(snap => snap.RecordingPrioritizedRelease.Status,NC_MB_RECPRIORITIZEDRELEASE)},
        {"music-brainz-track-id", new Mapping(snap => snap.ChosenTrack.Id,NC_MB_CHOSENTRACK)},
        {"track", new Mapping(snap => snap.ReleaseMedia.TrackOffset+1,NC_MB_RELEASEMEDIA)},
        {"track-count", new Mapping(snap => snap.ReleaseMedia.TrackCount,NC_MB_RELEASEMEDIA)},
        {"title", new Mapping(snap => snap.RecordingResponse.Title,NC_MB_RECORDINGRESPONSE)},
        {"year", new Mapping(snap =>
        {
            DateTime t;
            if (DateTime.TryParse(snap.RecordingPrioritizedRelease.ReleaseGroup.FirstReleaseDate, out t))
            {
                return t.Year.ToString();
            }
            else
            {
                return string.Empty;
            }
        },NC_MB_RECPRIORITIZEDRELEASE)},
    
        {"external-links", new Mapping(snap=>snap.ExternalLinks, NC_MB_RELEASERESPONSE)},
        {"image-link", new Mapping(snap=>snap.ImageLink, NC_CAA_RESPONSE)}
    };


    public HashSet<string> ValidProperties = new HashSet<string>();
    public HashSet<string> InvalidProperties = new HashSet<string>();
    public HashSet<string> MissingProperties = new HashSet<string>();
    
    
    // disc
    public int Disc = 1;
    
    // disc-count
    public int DiscCount = 1;
    
    // music-brainz-disc-id
    public string MusicBrainzDiscID = "";
    
    // external-links
    public List<Uri>? ExternalLinks = null;
    
    // image-link
    public Uri? ImageLink = null;
  
    
    
    
    
    // acoustid
    public AcoustID_APIResponse? AcoustIDResponse = null;
    
    // music-brainz-recording-id
    public string? RecordingID = null;
    
    
    // isrcs, length, title
    public MusicBrainz_APIResponse? RecordingResponse = null;
    
    // Release chosen based on a set of priorities
    //
    // album, album-artists, album-artists-sort, year,
    // music-brainz-release-id, music-brainz-release-status
    public MusicBrainz_APIResponse.Release? RecordingPrioritizedRelease = null;
    
    // A Media element from a prioritized release. Defaults to
    // the first element of media (if it exists at all)
    //
    // track-1, track-count
    public MusicBrainz_APIResponse.Media? ReleaseMedia = null;
    
    
    // external-links
    public MusicBrainz_APIResponse? ReleaseResponse = null;
    // Track acquired from list of the Release response
    // based on an offset
    //
    // artists, genres, track-id, music-brainz-artist-id
    public MusicBrainz_APIResponse.Track? ChosenTrack = null;
    
    
    
    // image-link (for now return 250px, then adjust to query needs)
    public CoverArtArchive_APIResponse? CoverArtArchiveResponse = null;
    
    



    /// <summary>
    /// Adjust needed components based on the present components.
    /// This method is introduced, so that components that rely on
    /// other components could make sure that the values to calculate
    /// the value of their own component are present.
    /// </summary>
    /// <param name="components"></param>
    /// <returns></returns>
    public static byte AdjustNeededComponents(byte components)
    {
        // A magic trick could've been computed on
        // this structure by taking the MSB and 
        // applying a bitwise OR on the components, 
        // however there are some relations (mostly
        // connected to RecordingPrioritizedRelease)
        // which do not allow equivalent transformation,
        // so:
        if ((components & NC_CAA_RESPONSE) != 0)
            components |= NC_MB_RECPRIORITIZEDRELEASE;

        if ((components & NC_MB_CHOSENTRACK) != 0)
        {
            components |= NC_MB_RELEASERESPONSE;
            components |= NC_MB_RELEASEMEDIA;
        }


        if ((components & NC_MB_RELEASERESPONSE) != 0)
            components |= NC_MB_RECPRIORITIZEDRELEASE;

        if ((components & NC_MB_RELEASEMEDIA) != 0 ||
            (components & NC_MB_RECPRIORITIZEDRELEASE) != 0)
            components |= NC_MB_RECORDINGRESPONSE;

        if ((components & NC_MB_RECORDINGRESPONSE) != 0)
            components |= NC_MB_RECORDINGID;

        if ((components & NC_MB_RECORDINGID) != 0)
            components |= NC_AID_RESPONSE;

        return components;
    }
}