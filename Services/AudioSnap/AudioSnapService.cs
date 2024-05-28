﻿using AudioSnapServer.Options;
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
    public AudioSnapService(IOptions<ExternalAPIClientOptions> options,
        IHttpClientFactory httpClientFactory,
        ILogger<AudioSnapService> logger)
    {
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public AudioSnap? GetSnapByHash()
    {
        return null;
    }

    public async Task<AudioSnap?> GetSnapByFingerprint(AudioSnapClientQuery query)
    {
        AudioSnap snap = new AudioSnap();
        
        AcoustID_APIResponse? AID_response = await AcoustID_QueryAsync(query.Fingerprint, query.DurationInSeconds);
        if (AID_response == null || 
            AID_response.Status!="ok" || 
            AID_response.Results.Count<1 || 
            AID_response.Results[0].Recordings.Count<1)
        {
            // logging already complete
            return null;
        }

        snap.AcoustIDResponse = AID_response;
        
        // choose the recording with the most score 
        // (assuming that it's the first one in the list)
        // TODO: check if the recording with the largest score really is the first one
        string recordingID = AID_response.Results[0].Recordings[0].RecordingID;
        
        //if(recordingID is not present in the database already)
        if (false)
        {
            // return result from the database
            return null;
        }
        
        // query MusicBrainz for this recording (by its ID)
        MusicBrainz_APIResponse MB_responseRecording =
            await MusicBrainz_QueryAsync(MusicBrainzEntry.E_Recording, recordingID);
        if (MB_responseRecording == null)
        {
            // logging already complete
            return null;
        }

        snap.RecordingResponse = MB_responseRecording;
            
        // applying preferences
        // (for now, choose first releaseID found)
        snap.RecordingPrioritizedRelease = MB_responseRecording.Releases[0];
        string preferredReleaseID = snap.RecordingPrioritizedRelease.Id;
        snap.ReleaseMedia = snap.RecordingPrioritizedRelease.Media[0];
        
        // if(releaseID is not present in the database already)
        if (false)
        {
            // return database entry
            return null;
        }
        
        // querying MusicBrainz for the release (by its ID)
        MusicBrainz_APIResponse MB_responseRelease =
            await MusicBrainz_QueryAsync(MusicBrainzEntry.E_Release, preferredReleaseID);
        if (MB_responseRelease == null)
        {
            // logging already complete
            return null;
        }

        snap.ReleaseResponse = MB_responseRelease;
        snap.ChosenTrack = snap.ReleaseResponse.ReleaseMedia[0].Tracks[snap.ReleaseMedia.TrackOffset];
        
        CoverArtArchive_APIResponse CAA_response = await CoverArtArchive_QueryAsync(preferredReleaseID);
        if (CAA_response == null)
        {
            // logging already complete
            return null;
        }

        snap.CoverArtArchiveResponse = CAA_response;

        return snap;
    }

    public void SaveSnap()
    {
        throw new NotImplementedException();
    }


    private async Task<AcoustID_APIResponse?> AcoustID_QueryAsync(string fingerprint, int duration)
    { 
        HttpClient AID_Client = _httpClientFactory.CreateClient("acoustid");
        HttpRequestMessage AID_request = 
            new HttpRequestMessage(
                HttpMethod.Get, 
                // FormQuery_AcoustID(_options.AcoustIDKey, 641,"AQABz0qUkZK4oOfhL-CPc4e5C_wW2H2QH9uDL4cvoT8UNQ-eHtsE8cceeFJx-LiiHT-aPzhxoc-Opj_eI5d2hOFyMJRzfDk-QSsu7fBxqZDMHcfxPfDIoPWxv9C1o3yg44d_3Df2GJaUQeeR-cb2HfaPNsdxHj2PJnpwPMN3aPcEMzd-_MeB_Ej4D_CLP8ghHjkJv_jh_UDuQ8xnILwunPg6hF2R8HgzvLhxHVYP_ziJX0eKPnIE1UePMByDJyg7wz_6yELsB8n4oDmDa0Gv40hf6D3CE3_wH6HFaxCPUD9-hNeF5MfWEP3SCGym4-SxnXiGs0mRjEXD6fgl4LmKWrSChzzC33ge9PB3otyJMk-IVC6R8MTNwD9qKQ_CC8kPv4THzEGZS8GPI3x0iGVUxC1hRSizC5VzoamYDi-uR7iKPhGSI82PkiWeB_eHijvsaIWfBCWH5AjjCfVxZ1TQ3CvCTclGnEMfHbnZFA8pjD6KXwd__Cn-Y8e_I9cq6CR-4S9KLXqQcsxxoWh3eMxiHI6TIzyPv0M43YHz4yte-Cv-4D16Hv9F9C9SPUdyGtZRHV-OHEeeGD--BKcjVLOK_NCDXMfx44dzHEiOZ0Z44Rf6DH5R3uiPj4d_PKolJNyRJzyu4_CTD2WOvzjKH9GPb4cUP1Av9EuQd8fGCFee4JlRHi18xQh96NLxkCgfWFKOH6WGeoe4I3za4c5hTscTPEZTES1x8kE-9MQPjT8a8gh5fPgQZtqCFj9MDvp6fDx6NCd07bjx7MLR9AhtnFnQ70GjOcV0opmm4zpY3SOa7HiwdTtyHa6NC4e-HN-OfC5-OP_gLe2QDxfUCz_0w9l65HiPAz9-IaGOUA7-4MZ5CWFOlIfe4yUa6AiZGxf6w0fFxsjTOdC6Itbh4mGD63iPH9-RFy909XAMj7mC5_BvlDyO6kGTZKJxHUd4NDwuZUffw_5RMsde5CWkJAgXnDReNEaP6DTOQ65yaD88HoeX8fge-DSeHo9Qa8cTHc80I-_RoHxx_UHeBxrJw62Q34Kd7MEfpCcu6BLeB1ePw6OO4sOF_sHhmB504WWDZiEu8sKPpkcfCT9xfej0o0lr4T5yNJeOvjmu40w-TDmqHXmYgfFhFy_M7tD1o0cO_B2ms2j-ACEEQgQgAIwzTgAGmBIKIImNQAABwgQATAlhDGCCEIGIIM4BaBgwQBogEBIOESEIA8ARI5xAhxEFmAGAMCKAURKQQpQzRAAkCCBQEAKkQYIYIQQxCixCDADCABMAE0gpJIgyxhEDiCKCCIGAEIgJIQByAhFgGACCACMRQEyBAoxQiHiCBCFOECQFAIgAABR2QAgFjCDMA0AUMIoAIMChQghChASGEGeYEAIAIhgBSErnJPPEGWYAMgw05AhiiGHiBBBGGSCQcQgwRYJwhDDhgCSCSSEIQYwILoyAjAIigBFEUQK8gAYAQ5BCAAjkjCCAEEMZAUQAZQCjCCkpCgFMCCiIcVIAZZgilAQAiSHQECOcQAQIc4QClAHAjDDGkAGAMUoBgyhihgEChFCAAWEIEYwIJYwViAAlHCBIGEIEAEIQAoBwwgwiEBAEEEOoEwBY4wRwxAhBgAcKAESIQAwwIowRFhoBhAE"));
                FormQuery_AcoustID(_options.AcoustIDKey, duration,fingerprint));

        AcoustID_APIResponse? acoustIdResponse = null;

        try
        {
            HttpResponseMessage httpResponse = await AID_Client.SendAsync(AID_request);
            if (!httpResponse.IsSuccessStatusCode)
            {
                // AcoustID response error
                _logger.LogError("AcoustID responded with status code other than success.");
                return null;
            }
            acoustIdResponse = await httpResponse.Content.ReadFromJsonAsync<AcoustID_APIResponse>();
        }
        catch (Exception ex)
        {
            _logger.LogError("AcoustID response error: "+ex.Message);
        }

        return acoustIdResponse;
        // _logger.LogInformation("AID response: "+await response.Content.ReadAsStringAsync());
    }

    private async Task<MusicBrainz_APIResponse?> MusicBrainz_QueryAsync(MusicBrainzEntry entry, string entryID)
    {        
        HttpClient MB_Client = _httpClientFactory.CreateClient("musicbrainz");
        MusicBrainzQuery query = _queryParameters[entry];
        HttpRequestMessage MB_requestRecording = new HttpRequestMessage(
            HttpMethod.Get, 
            FormQuery_MusicBrainz(
                // "recording", 
                query.Entry,
                // "774e840c-164b-434c-8f2e-359eab5b050d",
                entryID,
                query.Parameters
                ));
        
        MusicBrainz_APIResponse? musicBrainzResponse = null;

        try
        {
            HttpResponseMessage httpResponse = await MB_Client.SendAsync(MB_requestRecording);
            if (!httpResponse.IsSuccessStatusCode)
            {
                // MusicBrainz response error
                _logger.LogError("MusicBrainz responded with status code other than success.");
                return null;
            }
            musicBrainzResponse = await httpResponse.Content.ReadFromJsonAsync<MusicBrainz_APIResponse>();
        }
        catch (Exception ex)
        {
            _logger.LogError($"MusicBrainz response error for entry {entry}/{entryID}: "+ex.Message);
        }

        return musicBrainzResponse;
        // _logger.LogInformation("MB response: "+await response.Content.ReadAsStringAsync());
    }

    private async Task<CoverArtArchive_APIResponse?> CoverArtArchive_QueryAsync(string releaseID)
    {
        HttpClient CAA_Client = _httpClientFactory.CreateClient("coverartarchive");
        HttpRequestMessage CAA_request = new HttpRequestMessage(
            HttpMethod.Get,
            FormQuery_CoverArtArchive(
                // "9da5903b-0f7b-42e7-a48f-eb44cf7adaf9"
                releaseID
                )
            );

        CoverArtArchive_APIResponse? coverArtArchiveResponse = null;
        
        try
        {
            HttpResponseMessage httpResponse = await CAA_Client.SendAsync(CAA_request);
            if (!httpResponse.IsSuccessStatusCode)
            {
                // Cover Art Archive response error
                _logger.LogError("Cover Art Archive responded with status code other than success.");
                return null;
            }
            coverArtArchiveResponse = await httpResponse.Content.ReadFromJsonAsync<CoverArtArchive_APIResponse>();
        }
        catch (Exception ex)
        {
            _logger.LogError($"CoverArtArchive response error: "+ex.Message);
        }

        return coverArtArchiveResponse;
        // _logger.LogInformation("CAA response: "+await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Form a query string to AcoustID API that expects to receive only recording IDs
    /// </summary>
    private string FormQuery_AcoustID(string ClientKey, int duration, string fingerprint)
    {
        return $"lookup?client={ClientKey}&duration={duration}&fingerprint={fingerprint}&meta=recordingids&format=json";
    }

    private string FormQuery_MusicBrainz(string entry, string entryID, string queryParams)
    {
        return $"{entry}/{entryID}?inc={queryParams}&fmt=json";
    }

    private string FormQuery_CoverArtArchive(string releaseID)
    {
        return $"release/{releaseID}";
    }
}