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
    
    
    // how convenient would've this been if typedef existed
    private byte _neededComponents = 0;
    private byte _retrievedComponents = 0;
    private AudioSnap _audioSnap;
    
    public string ErrorMessage { get; private set; } = "";


    /// <summary>
    /// Analyzes the needed components list and distinguishes which
    /// of the properties current implementation of the service can
    /// retrieve, preparing to work with <see cref="CalculateSnap"/>
    /// </summary>
    public void SetNeededComponents(IEnumerable<string> searchParameters)
    {
        // analyze properties to include
        _audioSnap = new AudioSnap();
        
        ErrorMessage = "";
        _neededComponents = 0;
        _retrievedComponents = 0;
        
        foreach (string queryProperty in searchParameters)
        {
            if (AudioSnap.PropertyMappings.Keys.Contains(queryProperty))
            {
                _audioSnap.ValidProperties.Add(queryProperty);
                byte currMask = AudioSnap.PropertyMappings[queryProperty].Mask;
                if(currMask!=AudioSnap.NC_SPECIALPRESENT)
                    _neededComponents |= AudioSnap.PropertyMappings[queryProperty].Mask;
            }
            else
            {
                _audioSnap.InvalidProperties.Add(queryProperty);
            }
        }
        
        // adjust needed components
        _neededComponents = AudioSnap.AdjustNC(_neededComponents);
    }
    
    private void DetermineMissingProperties()
    {
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
        
        foreach (string property in _audioSnap.ValidProperties)
        {
            byte currMask = AudioSnap.PropertyMappings[property].Mask;
            if (currMask != AudioSnap.NC_SPECIALPRESENT &&
                (_retrievedComponents & currMask) == 0)
            {
                _audioSnap.MissingProperties.Add(property);
            }
        }

        _audioSnap.ValidProperties.Except(_audioSnap.MissingProperties);
    }

    public string GetSerializedResponse()
    {
        DetermineMissingProperties();

        // process image links and ext links
        if (_audioSnap.ValidProperties.Remove("image-link"))
        {
            // find it and assign a value
            _audioSnap.RESIMGLINK = _audioSnap.CoverArtArchiveResponse.Images[0].Thumbnails.Link250px;
        }

        if (_audioSnap.ValidProperties.Remove("external-links"))
        { 
            // find it and assign a value
            _audioSnap.RESEXTLINKS = _audioSnap.ReleaseResponse.ReleaseRelations.Select(rr => rr.url.Resource).ToList();
        }

        
        JsonSerializerOptions options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters =
            {
                new AudioSnapJsonConverter()
            }
        };
        
        string res = JsonSerializer.Serialize(_audioSnap, options);

        return res;
    }

    public void SetNecessaryComponents(IEnumerable<string> searchParameters)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Query the database and different APIs based on the
    /// needed components set in the bit-based value,
    /// calculated in <see cref="SetNeededComponents"/>
    /// </summary>
    /// <returns>
    /// True on success (<see cref="GetSerializedResponse"/> can be called succesfully),
    /// false on error (see <see cref="ErrorMessage"/> property for more details on the error)</returns>
    /// <remarks>
    /// In this case an error is considered something that normally
    /// shouldn't happen in normal recognition process, like receiving
    /// empty response, or not receiving any response at all. It must
    /// be distunguished from the cases where a property is not found
    /// (well, except for AcoustID response case, where there's literally
    /// nothing that can be found, so in that case return "track not found"
    /// as if the track is not known)
    /// </remarks>
    public async Task<bool> CalculateSnap(AudioSnapClientQuery query){
        bool status = true;
        // find the components
        
        // 1. AcoustID response
        _retrievedComponents = 0;
        if ((_neededComponents & AudioSnap.NC_AID_RESPONSE) != 0)
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
                    APIQueryBuilder.Q_AcoustID(_options.AcoustIDKey, query.DurationInSeconds, query.Fingerprint));
                if (snapAID == null || 
                    snapAID.Status!="ok" || 
                    snapAID.Results.Count<1 )
                {
                    // TODO: Provide meaningful error messages
                    snapAID = null;
                    if (status) ErrorMessage = "AcoustID error: failed to retrieve proper response";
                    status = false;
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
                _audioSnap.AcoustIDResponse = snapAID;
                _retrievedComponents |= AudioSnap.NC_AID_RESPONSE;
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
        if ((_neededComponents & AudioSnap.NC_MB_RECORDINGID) != 0 &&
            (_retrievedComponents & AudioSnap.NC_AID_RESPONSE) != 0)
        {
            if (_audioSnap.AcoustIDResponse.Results[0].Recordings.Count < 1)
            {
                if (status) ErrorMessage = "No recordings were found in AcoustID response";
                status = false;
            }
            else
            {
                // choose the recording with the most score 
                // (assuming that it's the first one in the list)
                // TODO: check if the recording with the largest score really is the first one
                _audioSnap.RecordingID = _audioSnap.AcoustIDResponse.Results[0].Recordings[0].RecordingID;
                _retrievedComponents |= AudioSnap.NC_MB_RECORDINGID;
            }
        }
        
        // 3. MusicBrainz Recording Response
        if ((_neededComponents & AudioSnap.NC_MB_RECORDINGRESPONSE) != 0 &&
            (_retrievedComponents & AudioSnap.NC_MB_RECORDINGID)!=0)
        {
            //if(recordingID is not present in the database already)
            MusicBrainz_APIResponse? snapMBRecording = null;

            IQueryable<RecordingStorage> dbQuery =
                from e in _dbContext.Recordings
                where e.RecordingID == _audioSnap.RecordingID
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
                    APIQueryBuilder.Q_MusicBrainz(
                        queryParams.Entry,
                        _audioSnap.RecordingID,
                        queryParams.Parameters
                    )
                    );
                if (snapMBRecording == null)
                {
                    // TODO: make reasonable error messages
                    if(status) ErrorMessage = "MusicBrainz error: response is null for recording query";
                    status = false;
                }
                else
                {
                    await _dbContext.Recordings.AddAsync(new RecordingStorage()
                    {
                        RecordingID = _audioSnap.RecordingID,
                        RecordingResponse = JsonSerializer.Serialize(snapMBRecording)
                    });
                    await _dbContext.SaveChangesAsync();
                }
            }

            if (snapMBRecording != null)
            {
                _retrievedComponents |= AudioSnap.NC_MB_RECORDINGRESPONSE;
                _audioSnap.RecordingResponse = snapMBRecording;
            }
        }

        // 4. Choosing prioritized release
        if ((_neededComponents & AudioSnap.NC_MB_RECPRIORITIZEDRELEASE) != 0 &&
            (_retrievedComponents & AudioSnap.NC_MB_RECORDINGRESPONSE) != 0)
        {
            if (_audioSnap.RecordingResponse.Releases.Count < 1)
            {
                if (status) ErrorMessage = "No releases were found in MusicBrainz response on recording query";
                status = false;
            }
            else
            {
                // applying preferences
                // (for now, choose first releaseID found)
                _retrievedComponents |= AudioSnap.NC_MB_RECPRIORITIZEDRELEASE;
                _audioSnap.RecordingPrioritizedRelease = _audioSnap.RecordingResponse.Releases[0];
            }
        }

        // 5. Validating release media component
        if ((_neededComponents & AudioSnap.NC_MB_RELEASEMEDIA) != 0 &&
            (_retrievedComponents & AudioSnap.NC_MB_RECORDINGRESPONSE)!=0)
        {
            // default to first media component
            if (_audioSnap.RecordingPrioritizedRelease.Media.Count < 1)
            {
                if (status) ErrorMessage = "No media content found in prioritized release";
                status = false;
            }
            else
            {
                _retrievedComponents |= AudioSnap.NC_MB_RELEASEMEDIA;
                _audioSnap.ReleaseMedia = _audioSnap.RecordingPrioritizedRelease.Media[0];
            }
        }
        
        
        // 6. Release response
        if ((_neededComponents & AudioSnap.NC_MB_RELEASERESPONSE) != 0 &&
            (_retrievedComponents & AudioSnap.NC_MB_RECPRIORITIZEDRELEASE) != 0)
        {
            string preferredReleaseID = _audioSnap.RecordingPrioritizedRelease.Id;
            // if(releaseID is not present in the database already)

            MusicBrainz_APIResponse? snapMBRelease = null;
            IQueryable<ReleaseDBResponse> dbQuery =
                from e in _dbContext.Releases
                where e.ReleaseID == preferredReleaseID
                select new ReleaseDBResponse(e.ReleaseID, e.ReleaseResponse);
            ReleaseDBResponse? dbMBRelease = dbQuery.FirstOrDefault();
            if(dbMBRelease!=null)
            {
                // return database entry
                if (dbMBRelease.ReleaseJson != null)
                {
                    snapMBRelease = JsonSerializer.Deserialize<MusicBrainz_APIResponse>(dbMBRelease.ReleaseJson);
                }
                else
                {
                    // error occurred: release comes before coverartarchive
                    // response, and is created firstly
                    string msg = "Database response on release has null release response field, which shouldn't happen.";
                    _logger.LogError(msg);
                    if (status) ErrorMessage = msg;
                    status = false;
                }

            }
            else
            {
                MusicBrainzEntry entry = MusicBrainzEntry.E_Release; 
                MusicBrainzQuery queryParams = _queryParameters[entry];
                snapMBRelease = await QueryAPI<MusicBrainz_APIResponse>(
                    "musicbrainz", 
                    APIQueryBuilder.Q_MusicBrainz(
                        queryParams.Entry,
                        preferredReleaseID,
                        queryParams.Parameters
                        )
                    );

                if (snapMBRelease == null)
                {
                    // TODO: make reasonable error messages
                    if (status) ErrorMessage = "MusicBrainz error: response is null for release query";
                    status = false;
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
                _retrievedComponents |= AudioSnap.NC_MB_RELEASERESPONSE;
                _audioSnap.ReleaseResponse = snapMBRelease;
            }

        }
        
        // 7. Choosing the track
        if ((_neededComponents & AudioSnap.NC_MB_CHOSENTRACK) != 0 &&
            (_retrievedComponents & AudioSnap.NC_MB_RECPRIORITIZEDRELEASE) != 0)
        {
            // defaulting to 0th release media component
            if (_audioSnap.ReleaseResponse.ReleaseMedia.Count < 1)
            {
                if (status) ErrorMessage = "No media components were found in release response";
                status = false;
            }
            else
            {
                // the Tracks list is expected to have at least offset+1 tracks
                _audioSnap.ChosenTrack = _audioSnap.ReleaseResponse.ReleaseMedia[0].Tracks[_audioSnap.ReleaseMedia.TrackOffset];
                _retrievedComponents |= AudioSnap.NC_MB_CHOSENTRACK;
            }
        }
        
        // 8. Retrieving cover art
        if ((_neededComponents & AudioSnap.NC_CAA_RESPONSE) != 0 &&
            (_retrievedComponents & AudioSnap.NC_MB_RECPRIORITIZEDRELEASE) != 0)
        {
            CoverArtArchive_APIResponse? snapCAA = null;
            string preferredReleaseID = _audioSnap.RecordingPrioritizedRelease.Id;

            IQueryable<CoverArtDBResponse> dbQuery =
                from e in _dbContext.Releases
                where e.ReleaseID == preferredReleaseID
                select new CoverArtDBResponse(e.ReleaseID, e.CoverResponse);

            CoverArtDBResponse? dbCAA = dbQuery.FirstOrDefault();
            if (dbCAA == null)
            {
                // error occurred. do not continue
                // the cover art requires to have some kind of info about the 
                // release
                string msg = "Database response for CoverArtArchive response is null, " +
                             "which shouldn't happen as CAA response must be available once " +
                             "the release is known after recording response";
                _logger.LogError(msg);
                if (status)
                    ErrorMessage = msg;
                status = false;
            }
            else
            {
                if (dbCAA.CoverArtJson != null)
                {
                    // take from db
                    snapCAA = JsonSerializer.Deserialize<CoverArtArchive_APIResponse>(dbCAA.CoverArtJson);
                }
                else
                {
                    snapCAA = await QueryAPI<CoverArtArchive_APIResponse>(
                        "coverartarchive",
                        APIQueryBuilder.Q_CoverArtArchive(preferredReleaseID)
                        );
                    if (snapCAA == null)
                    { 
                        if (status) ErrorMessage = "No media components were found in release response";
                        status = false;
                    }
                    else
                    {
                        string serializedCAA = JsonSerializer.Serialize(snapCAA);
                        await _dbContext.Releases
                            .Where(e => e.ReleaseID == preferredReleaseID)
                            .ExecuteUpdateAsync(e => e.SetProperty(p => p.CoverResponse, serializedCAA));
                        await _dbContext.SaveChangesAsync();
                    }
                }
            }

            if (snapCAA != null)
            {
                _audioSnap.CoverArtArchiveResponse = snapCAA;
                _retrievedComponents |= AudioSnap.NC_CAA_RESPONSE;
            }
        }
        
        
        
        return status;
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
}