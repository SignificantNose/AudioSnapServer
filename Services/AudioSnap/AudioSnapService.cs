using System.Collections.ObjectModel;
using System.Reflection;
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
    
    public List<string> ErrorMessages { get; private set; } = new List<string>();


    /// <summary>
    /// Analyzes the needed components list and distinguishes which
    /// of the properties current implementation of the service can
    /// retrieve, preparing to work with <see cref="CalculateSnapAsync"/>
    /// </summary>
    public void SetNeededComponents(AudioSnapClientQuery query)
    {
        _audioSnap = new AudioSnap();
        ErrorMessages.Clear();
        
        _neededComponents = 0;
        _retrievedComponents = 0;
        
        // For now, a simple validation tool on acquired properties
        // exists, the logic for checking if an image has been acquired
        // would've made things more complex imho. But at least now 
        // the server can support multiple types of requiring links,
        // both image and external: by setting a bool parameter and
        // by setting the required property in an array
        
        if (query.IncludeCover) query.ReleaseProperties.Add("image-link");
        if (query.IncludeExternalLinks) query.ReleaseProperties.Add("external-links");
        
        foreach (string queryProperty in query.ReleaseProperties)
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
        _neededComponents = AudioSnap.AdjustNeededComponents(_neededComponents);
    }
    
    /// <summary>
    /// Query the database and different APIs based on the
    /// needed components set in the bit-based value,
    /// calculated in <see cref="SetNeededComponents"/>
    /// </summary>
    /// <returns>
    /// True on success (<see cref="GetSerializedResponse"/> can be called succesfully),
    /// false on error (see <see cref="ErrorMessages"/> property for more details on the error)</returns>
    /// <remarks>
    /// In this case an error is considered something that normally
    /// shouldn't happen in normal recognition process, like receiving
    /// empty response, or not receiving any response at all. It must
    /// be distunguished from the cases where a property is not found
    /// (well, except for AcoustID response case, where there's literally
    /// nothing that can be found, so in that case return "track not found"
    /// as if the track is not known)
    /// </remarks>
    public async Task<bool> CalculateSnapAsync(AudioSnapClientQuery query){
        _retrievedComponents = 0;
        ErrorMessages.Clear();
        string? returnStatus;
        
        // find the components
        // 1. AcoustID response
        returnStatus = await QueryAcoustIDAsync(query);
        if (returnStatus != null) ErrorMessages.Add(returnStatus);

        // 2. MusicBrainz recording ID
        returnStatus = await RetrieveRecordingIDAsync();
        if (returnStatus != null) ErrorMessages.Add(returnStatus);
        
        // 3. MusicBrainz Recording Response
        returnStatus = await RetrieveMBRecordingResponseAsync();
        if (returnStatus != null) ErrorMessages.Add(returnStatus);

        // 4. Choosing prioritized release
        returnStatus = ChoosePrioritizedRelease();
        if (returnStatus != null) ErrorMessages.Add(returnStatus);

        // 5. Validating release media component
        returnStatus = ValidateReleaseMedia();
        if (returnStatus != null) ErrorMessages.Add(returnStatus);
        
        // 6. Release response
        returnStatus = await QueryMBReleaseAsync();
        if (returnStatus != null) ErrorMessages.Add(returnStatus);
        
        // 7. Choosing the track
        returnStatus = ChooseTrack();
        if (returnStatus != null) ErrorMessages.Add(returnStatus);
        
        // 8. Retrieving cover art
        returnStatus = await QueryCoverArtArchiveAsync();
        if (returnStatus != null) ErrorMessages.Add(returnStatus);
        
        return ErrorMessages.Count>0;
    }
    
    /// <summary>
    /// Forms a json-serialized string to send to the user as a response.
    /// Must be called after <see cref="CalculateSnapAsync"/> method.
    /// </summary>
    /// <param name="maxImageSize">
    /// Maximum supported image size (used only if image link is required)
    /// </param>
    public string GetSerializedResponse(int? maxImageSize = null)
    {
        DetermineMissingProperties();

        // process image links and ext links
        if (_audioSnap.ValidProperties.Remove("image-link"))
        {
            CoverArtArchive_APIResponse.Image? img = _audioSnap.CoverArtArchiveResponse.Images.Where(img => img.Types.Contains("Front")).FirstOrDefault();
            if (img == null)
            {
                _logger.LogError("ReleaseResponse responded that CAA has the front cover art," +
                                 " while the image information has not been found in CAA response");
            }
            else
            {
                int desiredImgSize = maxImageSize ?? 500;
                // _audioSnap.ImageLink = (Uri?)GetDesiredImageProperty(desiredImgSize).GetValue(img);
                _audioSnap.ImageLink = GetDesiredImageLink(img, desiredImgSize);
            }
        }

        if (_audioSnap.ValidProperties.Remove("external-links"))
        { 
            _audioSnap.ExternalLinks = _audioSnap.ReleaseResponse.ReleaseRelations.Select(rr => rr.url.Resource).ToList();
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
    
    
    #region Helper private methods
    private void DetermineMissingProperties()
    {
        // analyzing the retrieved components, then forming the list
        // of values that actually can be retrieved from the snap
        
        foreach (string property in _audioSnap.ValidProperties)
        {
            byte currMask = AudioSnap.PropertyMappings[property].Mask;
            if (currMask != AudioSnap.NC_SPECIALPRESENT &&
                (_retrievedComponents & currMask) == 0)
            {
                _audioSnap.MissingProperties.Add(property);
            }
        }

        _audioSnap.ValidProperties.ExceptWith(_audioSnap.MissingProperties);
    }
    
    private Uri GetDesiredImageLink(CoverArtArchive_APIResponse.Image img, int imgSize)
    {
        // string outerPropertyName = "Thumbnails";
        // string propertyName;
        Uri imgLink;
        
        switch (imgSize)
        {
            case < 500:
                // propertyName = "Thumbnails.Link250px";
                imgLink = img.Thumbnails.Link250px;
                break;
            case < 1200:
                // propertyName = "Thumbnails.Link500px";
                imgLink = img.Thumbnails.Link500px;
                break;
            default:
                // propertyName = "Thumbnails.Link1200px";
                imgLink = img.Thumbnails.Link1200px;
                break;
        }

        // return typeof(CoverArtArchive_APIResponse.Image).GetProperty(propertyName);
        return imgLink;
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
    #endregion

    #region Query Stages
    
    // 1. AcoustID response
    private async Task<string?> QueryAcoustIDAsync(AudioSnapClientQuery query)
    {
        if ((_neededComponents & AudioSnap.NC_AID_RESPONSE) != 0)
        {
            // calculate the hash
            byte[] acquiredData = ChromaBase64.ByteEncoding.GetBytes(query.Fingerprint);
            int[] fingerprint = IFileChromaContext.DecodeFingerprint(acquiredData, true, out _);
            uint fpHash = SimHash.Compute(fingerprint);
    
            // search it in the database
            // int bitThreshold = 3;
            int durationThreshold = 10;

            IOrderedQueryable<AcoustIDStorage> dbQuery = from e in _dbContext.AcoustIDs
                where _dbContext.GetAbsDiff(e.Duration,query.DurationInSeconds)<durationThreshold //&& 
                      // _dbContext.GetBitDiff(fpHash, e.Hash )<bitThreshold &&
                      // (_dbContext.GetBitDiff(fpHash, e.Hash)/(double)sizeof(uint))*e.MatchingScore > query.MatchingRate
                orderby ((double)_dbContext.GetBitDiff(fpHash, e.Hash)/sizeof(uint))*e.MatchingScore
                select e;
            
            AcoustIDStorage? dbResult = dbQuery.FirstOrDefault();
            
            
            AcoustID_APIResponse? snapAID = null;
            if (dbResult != null)
            {
                // don't know any other way to take the calculated value from the query,
                // especially how to make a variable in SQL query, especially using linq
                double matchingScore = (double)((sizeof(uint)<<3)-uint.PopCount(dbResult.Hash ^ fpHash)) / (sizeof(uint)<<3)*dbResult.MatchingScore;

                if (matchingScore >= query.MatchingRate)
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
            }
            
            
            if (snapAID==null)
            {
                
                snapAID = await QueryAPI<AcoustID_APIResponse>
                ("acoustid",
                    APIQueryBuilder.Q_AcoustID(_options.AcoustIDKey, query.DurationInSeconds, query.Fingerprint));
                if (snapAID == null || 
                    snapAID.Status!="ok" || 
                    snapAID.Results.Count<1 )
                {
                    // TODO: Provide meaningful error messages
                    // snapAID = null;
                    // if (status) ErrorMessage = "AcoustID error: failed to retrieve proper response";
                    // status = false;
                    return "AcoustID error: failed to retrieve proper response";
                }
                else
                {

                    AcoustID_APIResponse.Result mostScoredResult = 
                        snapAID.Results.OrderByDescending(e => e.MatchScore).First();
                    if (mostScoredResult.Recordings.Count < 1)
                    {
                        // unexpected error
                        string msg = $"AcoustID response with no recording IDs found: {JsonSerializer.Serialize(snapAID)}"; 
                        _logger.LogError(msg);
                        return msg;
                    }
                    else
                    {
                        string mostScoredRecordingId = mostScoredResult.Recordings[0].RecordingID;
                        
                        // if the acquired result exists in the database and its matching score
                        // is larger than the one stored in a database, update the database entry
                        AcoustIDStorage? entry = _dbContext.AcoustIDs.Find(mostScoredResult.AcoustID_ID);

                        if (entry != null)
                        {
                            // entry found. Look at its matching rate
                            if (entry.MatchingScore < mostScoredResult.MatchScore)
                            {
                                // update the database entry
                                entry.MatchingScore = mostScoredResult.MatchScore;
                                entry.RecordingID = mostScoredRecordingId;
                                entry.Duration = query.DurationInSeconds;
                                entry.Hash = fpHash;
                            }
                            
                            // use the database entry as the response
                            snapAID = new AcoustID_APIResponse(
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
                        else
                        {
                            await _dbContext.AcoustIDs.AddAsync(new AcoustIDStorage()
                            {
                                Hash = fpHash,
                                AcoustID = mostScoredResult.AcoustID_ID,
                                Duration = query.DurationInSeconds,
                                MatchingScore = mostScoredResult.MatchScore,
                                RecordingID = mostScoredRecordingId
                            });
                        }

                        await _dbContext.SaveChangesAsync();
                        
                        // A slippery moment here: if the score is low, the result will still
                        // be stored in a database, no matter the user constraints. And further
                        // requests which would appear more similar to the actually true value
                        // of the fingerprint, will be not alike with the stored result, but
                        // would actually be more alike with the actual result.
                        // 
                        // upd: solved: update the database with more relevant results
                        
                        
                        // finally, check the acquired matching score
                        if (mostScoredResult.MatchScore < query.MatchingRate)
                        {
                            // couldn't satisfy client's needs
                            snapAID = null;
                        }
                    }
                }
            }
            
            if(snapAID!=null)
            {
                _audioSnap.AcoustIDResponse = snapAID;
                _retrievedComponents |= AudioSnap.NC_AID_RESPONSE;
            }
        }
        return null;
    }
    
    // 2. MusicBrainz recording ID
    private async Task<string?> RetrieveRecordingIDAsync()
    {
        // maybe looking at the value of retrievedComponents is
        // kind of redundant and can be resolved with one bool
        // variable, but I think that it makes code look more
        // readable & logical
        // 
        // moreover, if any error occurred beforehand, it will
        // not cause errors later on
        
        // I'm checking the easily-acquirable value, even if it is
        // not needed, so that if any errors related to incorrect
        // mapping of properties come up
        if ((_neededComponents & AudioSnap.NC_MB_RECORDINGID) != 0 &&
            (_retrievedComponents & AudioSnap.NC_AID_RESPONSE) != 0)
        {
            if (_audioSnap.AcoustIDResponse.Results[0].Recordings.Count < 1)
            {
                // if (status) ErrorMessage = "No recordings were found in AcoustID response";
                // status = false;
                return "No recordings were found in AcoustID response";
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

        return null;
    }

    // 3. MusicBrainz Recording Response
    private async Task<string?> RetrieveMBRecordingResponseAsync()
    {
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
                snapMBRecording = await QueryAPI<MusicBrainz_APIResponse>(
                    "musicbrainz",
                    APIQueryBuilder.Q_MusicBrainz(
                        APIQueryBuilder.MusicBrainzEntry.ERecording,
                        _audioSnap.RecordingID
                    )
                    );
                if (snapMBRecording == null)
                {
                    // TODO: make reasonable error messages
                    // if(status) ErrorMessage = "MusicBrainz error: response is null for recording query";
                    // status = false;
                    return "MusicBrainz error: response is null for recording query";
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

        return null;
    }
    
    // 4. Choosing prioritized release
    private string? ChoosePrioritizedRelease()
    {
        if ((_neededComponents & AudioSnap.NC_MB_RECPRIORITIZEDRELEASE) != 0 &&
            (_retrievedComponents & AudioSnap.NC_MB_RECORDINGRESPONSE) != 0)
        {
            if (_audioSnap.RecordingResponse.Releases.Count < 1)
            {
                // if (status) ErrorMessage = "No releases were found in MusicBrainz response on recording query";
                // status = false;
                return "No releases were found in MusicBrainz response on recording query";
            }
            else
            {
                // applying preferences
                // (for now, choose first releaseID found)
                _retrievedComponents |= AudioSnap.NC_MB_RECPRIORITIZEDRELEASE;
                _audioSnap.RecordingPrioritizedRelease = _audioSnap.RecordingResponse.Releases[0];
            }
        }

        return null;
    }
    
    // 5. Validating release media component
    private string? ValidateReleaseMedia()
    {
        if ((_neededComponents & AudioSnap.NC_MB_RELEASEMEDIA) != 0 &&
            (_retrievedComponents & AudioSnap.NC_MB_RECORDINGRESPONSE)!=0)
        {
            // default to first media component
            if (_audioSnap.RecordingPrioritizedRelease.Media.Count < 1)
            {
                // if (status) ErrorMessage = "No media content found in prioritized release";
                // status = false;
                return "No media content found in prioritized release";

            }
            else
            {
                _retrievedComponents |= AudioSnap.NC_MB_RELEASEMEDIA;
                _audioSnap.ReleaseMedia = _audioSnap.RecordingPrioritizedRelease.Media[0];
            }
        }

        return null;
    }

    // 6. Release response
    private async Task<string?> QueryMBReleaseAsync()
    {
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
                    // if (status) ErrorMessage = msg;
                    // status = false;
                    return msg;
                }

            }
            else
            {
                snapMBRelease = await QueryAPI<MusicBrainz_APIResponse>(
                    "musicbrainz", 
                    APIQueryBuilder.Q_MusicBrainz(
                        APIQueryBuilder.MusicBrainzEntry.ERelease,
                        preferredReleaseID
                        )
                    );

                if (snapMBRelease == null)
                {
                    // TODO: make reasonable error messages
                    // if (status) ErrorMessage = "MusicBrainz error: response is null for release query";
                    // status = false;
                    return "MusicBrainz error: response is null for release query";
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

        return null;
    }

    private string? ChooseTrack()
    {
        if ((_neededComponents & AudioSnap.NC_MB_CHOSENTRACK) != 0 &&
            (_retrievedComponents & AudioSnap.NC_MB_RECPRIORITIZEDRELEASE) != 0)
        {
            // defaulting to 0th release media component
            if (_audioSnap.ReleaseResponse.ReleaseMedia.Count < 1)
            {
                // if (status) ErrorMessage = "No media components were found in release response";
                // status = false;
                return "No media components were found in release response";
            }
            else
            {
                // the Tracks list is expected to have at least offset+1 tracks
                _audioSnap.ChosenTrack = _audioSnap.ReleaseResponse.ReleaseMedia[0].Tracks[_audioSnap.ReleaseMedia.TrackOffset];
                _retrievedComponents |= AudioSnap.NC_MB_CHOSENTRACK;
            }
        }

        return null;
    }

    private async Task<string?> QueryCoverArtArchiveAsync()
    {
        if ((_neededComponents & AudioSnap.NC_CAA_RESPONSE) != 0 &&
            (_retrievedComponents & AudioSnap.NC_MB_RECPRIORITIZEDRELEASE) != 0)
        {
            if (_audioSnap.ReleaseResponse.CAAInfo.IsFrontAvailable)
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
                    // if (status) ErrorMessage = msg;
                    // status = false;
                    
                    return msg;
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
                            // if (status) ErrorMessage = "No media components were found in release response";
                            // status = false;
                            return "No media components were found in release response";
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
        }

        return null;
    }

    #endregion
}
