using System.Collections.ObjectModel;
using System.Text.Json;
using AudioSnapServer.Data;
using AudioSnapServer.Models.ResponseStorage;
using AudioSnapServer.Options;
using Chromaprint;
using Chromaprint.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AudioSnapServer.Services.AudioSnap;
using AudioSnapServer.Models;

public class AudioSnapService : IAudioSnapService
{
    private enum MusicBrainzEntry
    {
        E_Recording,
        E_Release
    };

    private record class MusicBrainzQuery(
        string Entry, string Parameters);

    private static readonly Dictionary<MusicBrainzEntry, MusicBrainzQuery> _queryParameters =
        new Dictionary<MusicBrainzEntry,MusicBrainzQuery>()
        {
            { MusicBrainzEntry.E_Recording, new MusicBrainzQuery("recording", "artist-credits+isrcs+releases+url-rels+genres+release-groups+discids") },
            { MusicBrainzEntry.E_Release, new MusicBrainzQuery("release", "artist-credits+labels+discids+recordings+url-rels") }
        };
    
    private ExternalAPIClientOptions _options;
    private IHttpClientFactory _httpClientFactory;
    private ILogger<AudioSnapService> _logger;
    private AudioSnapDbContext _dbContext;
    public AudioSnapService(IOptions<ExternalAPIClientOptions> options,
        IHttpClientFactory httpClientFactory,
        ILogger<AudioSnapService> logger,
        AudioSnapDbContext dbContext)
    {
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _dbContext = dbContext;
    }

    public AudioSnap? GetSnapByHash()
    {
        return null;
    }
    
    public async Task<AudioSnap?> GetSnapByFingerprint(AudioSnapClientQuery query)
    {
        AudioSnap snap = new AudioSnap();
        snap.response = new SnapUserResponseData();
        
        // analyze properties to include
        
        HashSet<string> validProperties = new HashSet<string>();
        HashSet<string> invalidProperties = new HashSet<string>();
        
        // how convenient would've this been if typedef existed
        byte neededComponents = 0;
        foreach (string queryProperty in query.ReleaseProperties)
        {
            if (AudioSnap.PropertyMappings.Keys.Contains(queryProperty))
            {
                validProperties.Add(queryProperty);
                byte currMask = AudioSnap.PropertyMappings[queryProperty].Mask;
                if(currMask!=AudioSnap.NC_SPECIALPRESENT)
                    neededComponents |= AudioSnap.PropertyMappings[queryProperty].Mask;
            }
            else
            {
                invalidProperties.Add(queryProperty);
            }
        }
        
        // adjust needed components
        neededComponents = AudioSnap.AdjustNC(neededComponents);
        
        
        // find the components
        
        // 1. AcoustID response
        byte retrievedComponents = 0;
        if ((neededComponents & AudioSnap.NC_AID_RESPONSE) != 0)
        {
            // calculate the hash
            byte[] acquiredData = ChromaBase64.ByteEncoding.GetBytes(query.Fingerprint);
            int[] fingerprint = IFileChromaContext.DecodeFingerprint(acquiredData, true, out _);
            uint fpHash = SimHash.Compute(fingerprint);
    
            // search it in the database
            int bitThreshold = 3;
            int durationThreshold = 10;

            IOrderedQueryable<AcoustIDStorage> dbQuery = from e in _dbContext.AcoustIDs
                where _dbContext.GetAbsDiff(e.Duration,query.DurationInSeconds)<durationThreshold && 
                      _dbContext.GetBitDiff(e.Hash, fpHash)<bitThreshold 
                orderby e.MatchingScore
                select e;

            AcoustIDStorage? dbResult = dbQuery.FirstOrDefault();
            AcoustID_APIResponse? snapAID = null;
            if (dbResult != null)
            {
                // make it so
                snapAID = new AcoustID_APIResponse(
                    Results: new List<AcoustID_APIResponse.Result>()
                    {
                        new AcoustID_APIResponse.Result(
                            dbResult.AcoustID,
                            dbResult.MatchingScore,
                            new List<AcoustID_APIResponse.Recording>()
                            {
                                new AcoustID_APIResponse.Recording(dbResult.RecordingID)
                            }
                        )
                    },
                    Status: "ok",
                    QueryError: null
                    );
            }
            else
            {
                snapAID = await QueryAPI<AcoustID_APIResponse>
                    ("acoustid", 
                        FormQuery_AcoustID(_options.AcoustIDKey, query.DurationInSeconds, query.Fingerprint));
                if (snapAID == null || 
                    snapAID.Status!="ok" || 
                    snapAID.Results.Count<1 )
                {
                    // TODO: Provide meaningful error messages
                    snapAID = null;
                    snap.response.Status = false;
                    snap.response.ErrorMessage = "AcoustID error: failed to retrieve proper response";
                }
                else
                {
                    await _dbContext.AcoustIDs.AddAsync(new AcoustIDStorage()
                    {
                        Hash = fpHash,
                        AcoustID = snapAID.Results[0].AcoustID_ID,
                        Duration = query.DurationInSeconds,
                        MatchingScore = snapAID.Results[0].MatchScore,
                        RecordingID = snapAID.Results[0].Recordings[0].RecordingID
                    });
                    await _dbContext.SaveChangesAsync();
                }

            }

            
            if(snapAID!=null)
            {
                snap.AcoustIDResponse = snapAID;
                retrievedComponents |= AudioSnap.NC_AID_RESPONSE;
            }
            
        }
        
        
        // maybe looking at the value of retrievedComponents is
        // kind of redundant and can be resolved with one bool
        // variable, but I think that it makes code look more
        // readable & logical
        // 
        // moreover, if any error occurred beforehand, it will
        // not cause errors later on
        
        // 2. MusicBrainz recording ID
        // I'm checking the easily-acquirable value, even if it is
        // not needed, so that if any errors related to incorrect
        // mapping of properties come up
        if ((neededComponents & AudioSnap.NC_MB_RECORDINGID) != 0 &&
            (retrievedComponents & AudioSnap.NC_AID_RESPONSE) != 0)
        {
            if (snap.AcoustIDResponse.Results[0].Recordings.Count < 1)
            {
                snap.response.Status = false;
                snap.response.ErrorMessage = "No recordings were found in AcoustID response";
            }
            else
            {
                // choose the recording with the most score 
                // (assuming that it's the first one in the list)
                // TODO: check if the recording with the largest score really is the first one
                snap.RecordingID = snap.AcoustIDResponse.Results[0].Recordings[0].RecordingID;
                retrievedComponents |= AudioSnap.NC_MB_RECORDINGID;
            }
        }
        
        // 3. MusicBrainz Recording Response
        if ((neededComponents & AudioSnap.NC_MB_RECORDINGRESPONSE) != 0 &&
            (retrievedComponents & AudioSnap.NC_MB_RECORDINGID)!=0)
        {
            //if(recordingID is not present in the database already)
            MusicBrainz_APIResponse? snapMBRecording = null;

            IQueryable<RecordingStorage> dbQuery =
                from e in _dbContext.Recordings
                where e.RecordingID == snap.RecordingID
                select e;
            
            RecordingStorage? dbMBRecording = dbQuery.FirstOrDefault(); 
            if (dbMBRecording!=null)
            {
                // return result from the database
                snapMBRecording = JsonSerializer.Deserialize<MusicBrainz_APIResponse>(dbMBRecording.RecordingResponse);
            }
            else
            {
                MusicBrainzEntry entry = MusicBrainzEntry.E_Recording;
                MusicBrainzQuery queryParams = _queryParameters[entry];
                snapMBRecording = await QueryAPI<MusicBrainz_APIResponse>(
                    "musicbrainz",
                    FormQuery_MusicBrainz(
                        queryParams.Entry,
                        snap.RecordingID,
                        queryParams.Parameters
                    )
                    );
                if (snapMBRecording == null)
                {
                    // TODO: make reasonable error messages
                    snap.response.Status = false;
                    snap.response.ErrorMessage = "MusicBrainz error: response is null for recording query";
                }
                else
                {
                    await _dbContext.Recordings.AddAsync(new RecordingStorage()
                    {
                        RecordingID = snap.RecordingID,
                        RecordingResponse = JsonSerializer.Serialize(snapMBRecording)
                    });
                    await _dbContext.SaveChangesAsync();
                }
            }

            if (snapMBRecording != null)
            {
                retrievedComponents |= AudioSnap.NC_MB_RECORDINGRESPONSE;
                snap.RecordingResponse = snapMBRecording;
            }
        }

        // 4. Choosing prioritized release
        if ((neededComponents & AudioSnap.NC_MB_RECPRIORITIZEDRELEASE) != 0 &&
            (retrievedComponents & AudioSnap.NC_MB_RECORDINGRESPONSE) != 0)
        {
            if (snap.RecordingResponse.Releases.Count < 1)
            {
                snap.response.Status = false;
                snap.response.ErrorMessage = "No releases were found in MusicBrainz response on recording query";
            }
            else
            {
                // applying preferences
                // (for now, choose first releaseID found)
                retrievedComponents |= AudioSnap.NC_MB_RECPRIORITIZEDRELEASE;
                snap.RecordingPrioritizedRelease = snap.RecordingResponse.Releases[0];
            }
        }

        // 5. Validating release media component
        if ((neededComponents & AudioSnap.NC_MB_RELEASEMEDIA) != 0 &&
            (retrievedComponents & AudioSnap.NC_MB_RECORDINGRESPONSE)!=0)
        {
            // default to first media component
            if (snap.RecordingPrioritizedRelease.Media.Count < 1)
            {
                snap.response.Status = false;
                snap.response.ErrorMessage = "No media content found in prioritized release";
            }
            else
            {
                retrievedComponents |= AudioSnap.NC_MB_RELEASEMEDIA;
                snap.ReleaseMedia = snap.RecordingPrioritizedRelease.Media[0];
            }
        }
        
        
        // 6. Release response
        if ((neededComponents & AudioSnap.NC_MB_RELEASERESPONSE) != 0 &&
            (retrievedComponents & AudioSnap.NC_MB_RECPRIORITIZEDRELEASE) != 0)
        {
            string preferredReleaseID = snap.RecordingPrioritizedRelease.Id;
            // if(releaseID is not present in the database already)

            MusicBrainz_APIResponse? snapMBRelease = null;
            IQueryable<ReleaseStorage> dbQuery =
                from e in _dbContext.Releases
                where e.ReleaseID == preferredReleaseID
                select e;
            ReleaseStorage? dbMBRelease = dbQuery.FirstOrDefault();
            if(dbMBRelease!=null)
            {
                // return database entry
                if (dbMBRelease.ReleaseResponse != null)
                {
                    snapMBRelease = JsonSerializer.Deserialize<MusicBrainz_APIResponse>(dbMBRelease.ReleaseResponse);
                }
                else
                {
                    // error occurred: release comes before coverartarchive
                    // response, and is created firstly
                    _logger.LogError("Database response on release has null release response field, which shouldn't happen.");
                }

            }
            else
            {
                MusicBrainzEntry entry = MusicBrainzEntry.E_Release; 
                MusicBrainzQuery queryParams = _queryParameters[entry];
                snapMBRelease = await QueryAPI<MusicBrainz_APIResponse>(
                    "musicbrainz", 
                    FormQuery_MusicBrainz(
                        queryParams.Entry,
                        preferredReleaseID,
                        queryParams.Parameters
                        )
                    );

                if (snapMBRelease == null)
                {
                    // TODO: make reasonable error messages
                    snap.response.Status = false;
                    snap.response.ErrorMessage = "MusicBrainz error: response is null for release query";
                }
                else
                {
                    await _dbContext.Releases.AddAsync(new ReleaseStorage()
                    {
                        ReleaseID = preferredReleaseID,
                        CoverResponse = null,
                        ReleaseResponse = JsonSerializer.Serialize(snapMBRelease)
                    });

                    await _dbContext.SaveChangesAsync();
                }
            }

            if (snapMBRelease != null)
            {
                retrievedComponents |= AudioSnap.NC_MB_RELEASERESPONSE;
                snap.ReleaseResponse = snapMBRelease;
            }

        }
        
        // 7. Choosing the track
        if ((neededComponents & AudioSnap.NC_MB_CHOSENTRACK) != 0 &&
            (retrievedComponents & AudioSnap.NC_MB_RECPRIORITIZEDRELEASE) != 0)
        {
            // defaulting to 0th release media component
            if (snap.ReleaseResponse.ReleaseMedia.Count < 1)
            {
                snap.response.Status = false;
                snap.response.ErrorMessage = "No media components were found in release response";
            }
            else
            {
                // the Tracks list is expected to have at least offset+1 tracks
                snap.ChosenTrack = snap.ReleaseResponse.ReleaseMedia[0].Tracks[snap.ReleaseMedia.TrackOffset];
                retrievedComponents |= AudioSnap.NC_MB_CHOSENTRACK;
            }
        }
        
        // 8. Retrieving cover art
        if ((neededComponents & AudioSnap.NC_CAA_RESPONSE) != 0 &&
            (retrievedComponents & AudioSnap.NC_MB_RECPRIORITIZEDRELEASE) != 0)
        {
            CoverArtArchive_APIResponse? snapCAA = null;
            string preferredReleaseID = snap.RecordingPrioritizedRelease.Id;

            IQueryable<ReleaseStorage> dbQuery =
                from e in _dbContext.Releases
                where e.ReleaseID == preferredReleaseID
                select e;

            ReleaseStorage? dbCAA = dbQuery.FirstOrDefault();
            if (dbCAA == null)
            {
                // error occurred. do not continue
                // the cover art requires to have some kind of info about the 
                // release
                _logger.LogError("Database response for CoverArtArchive response is null, " +
                                 "which shouldn't happen as CAA response must be available once " +
                                 "the release is known after recording response");
            }
            else
            {
                if (dbCAA.CoverResponse != null)
                {
                    // take from db
                    snapCAA = JsonSerializer.Deserialize<CoverArtArchive_APIResponse>(dbCAA.CoverResponse);
                }
                else
                {
                    snapCAA = await QueryAPI<CoverArtArchive_APIResponse>(
                        "coverartarchive",
                        FormQuery_CoverArtArchive(preferredReleaseID)
                        );
                    if (snapCAA == null)
                    {
                        snap.response.Status = false;
                        snap.response.ErrorMessage = "No media components were found in release response";
                    }
                    else
                    {
                        dbCAA.CoverResponse = JsonSerializer.Serialize(snapCAA);
                        await _dbContext.SaveChangesAsync();
                    }
                }

            }

            if (snapCAA != null)
            {
                snap.CoverArtArchiveResponse = snapCAA;
                retrievedComponents |= AudioSnap.NC_CAA_RESPONSE;
            }
        }
        
        // analyzing the retrieved components, then forming the list
        // of values that actually can be retrieved from the snap

        // List<string> missingProperties = new List<string>();
        // missingProperties.AddRange(validProperties.Where(property =>
        // {
            // byte currMask = AudioSnap.PropertyMappings[property].Mask;
            // return (currMask != AudioSnap.NC_SPECIALPRESENT &&
                    // (retrievedComponents & currMask) == 0);
        // }));
        // validProperties.RemoveAll(property => missingProperties.Contains(property));
        
        HashSet<string> missingProperties = new HashSet<string>();
        foreach (string property in validProperties)
        {
            byte currMask = AudioSnap.PropertyMappings[property].Mask;
            if (currMask != AudioSnap.NC_SPECIALPRESENT &&
                (retrievedComponents & currMask) == 0)
            {
                missingProperties.Add(property);
            }
        }

        validProperties.Except(missingProperties);

        // filling in the response
        snap.response.InvalidProperties = invalidProperties;
        snap.response.MissingProperties = missingProperties;
        snap.response.ValidProperties = validProperties;
        return snap;
    }

    public void SaveSnap()
    {
        throw new NotImplementedException();
    }


    private async Task<API_Response?> QueryAPI<API_Response>(string httpClientName, string QueryUri)
    {
        HttpClient httpClient = _httpClientFactory.CreateClient(httpClientName);
        HttpRequestMessage request = 
            new HttpRequestMessage(
                HttpMethod.Get, 
                QueryUri
                );

        API_Response? typedResponse = default(API_Response);

        try
        {
            HttpResponseMessage httpResponse = await httpClient.SendAsync(request);
            if (!httpResponse.IsSuccessStatusCode)
            {
                _logger.LogError($"{httpClientName} client: API responded with status code other than success.");
                return default(API_Response);
            }
            typedResponse = await httpResponse.Content.ReadFromJsonAsync<API_Response>();
        }
        catch (Exception ex)
        {
            _logger.LogError($"{httpClientName} client: response error: "+ex.Message);
            typedResponse = default(API_Response);
        }

        return typedResponse;
    }

    /// <summary>
    /// Form a query string to AcoustID API that expects to receive only recording IDs
    /// </summary>
    private string FormQuery_AcoustID(string ClientKey, int duration, string fingerprint)
    {
        return $"lookup?client={ClientKey}&duration={duration}&fingerprint={fingerprint}&meta=recordingids&format=json";
    }
    
    /// <summary>
    /// Form a query string to MusicBrainz API service for a specific entry
    /// </summary>
    private string FormQuery_MusicBrainz(string entry, string entryID, string queryParams)
    {
        return $"{entry}/{entryID}?inc={queryParams}&fmt=json";
    }

    /// <summary>
    /// Acquire Cover Art information for a release from CoverArtArchive API service
    /// </summary>
    private string FormQuery_CoverArtArchive(string releaseID)
    {
        return $"release/{releaseID}";
    }
}