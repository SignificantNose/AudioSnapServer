﻿using System.Text.Json.Serialization;

namespace AudioSnapServer.Models;

public record class MusicBrainz_APIResponse(
    // release-groups -> title                                (album)
    // release-groups -> artist-credit -> artist -> name      (album-artists)
    // release-groups -> artist-credit -> artist -> sort-name (album-artists-sort)
    // release-groups -> first-release-date                   (year)
    // id                                                     (music-brainz-release-id)
    // status                                                 (music-brainz-release-status)
    [property: JsonPropertyName("releases")] List<MusicBrainz_APIResponse.Release> Releases,
    
    // (isrcs)
    [property: JsonPropertyName("isrcs")] List<string> ISRCs,

    // (length)
    [property: JsonPropertyName("length")] int LengthMs,
    
    // (title)
    [property: JsonPropertyName("title")] string Title,
    
    
    
    
    [property: JsonPropertyName("media")] List<MusicBrainz_APIResponse.Media> ReleaseMedia
    )
{
    public record class Release(
        // title                                (album)
        // artist-credit -> artist -> name      (album-artists)
        // artist-credit -> artist -> sort-name (album-artists-sort)
        // first-release-date                   (year)
        [property: JsonPropertyName("release-group")] ReleaseGroup ReleaseGroup,
        
        // (music-brainz-release-id)
        [property: JsonPropertyName("id")] string Id,
        // (music-brainz-release-status)
        [property: JsonPropertyName("status")] string Status,
        
        // tracks -> artist -> name           (artists)
        // track-offset                       (track-1)
        // track-count                        (track-count)
        [property: JsonPropertyName("media")] Media Media,
        
        // prioritization
        [property: JsonPropertyName("country")] string Country
        );

    public record class ReleaseGroup(
        // prioritization
        [property: JsonPropertyName("primary-type")] string ReleaseType,    
        
        // (album)
        [property: JsonPropertyName("title")] string Title,

        // artist -> name      (album-artists)
        // artist -> sort-name (album-artists-sort)
        [property: JsonPropertyName("artist-credit")] List<ArtistCredit> ArtistCredits,
        
        // (year)
        [property: JsonPropertyName("first-release-date")] string FirstReleaseDate
        );

    public record class ArtistCredit(
        // name      (album-artists)
        // sort-name (album-artists-sort)
        [property: JsonPropertyName("artist")] Artist Artist
        );

    public record class Artist(
        // (album-artists)
        [property: JsonPropertyName("name")] string Name,
        
        // (album-artists-sort)
        [property: JsonPropertyName("sort-name")] string SortName,
        
        // (music-brainz-artist-id)
        [property: JsonPropertyName("id")] string Id
        );





    public record class Media(
        // artist -> name           (artists)
        [property: JsonPropertyName("tracks")] List<Track> Tracks,
        // (track-1)
        [property: JsonPropertyName("track-offset")] int TrackOffset,
        // (track-count)
        [property: JsonPropertyName("track-count")] int TrackCount
        );

    public record class Track(
        // artist -> name           (artists)
        [property: JsonPropertyName("artist-credit")] List<ArtistCredit> ArtistCredits,
        // (genres)
        [property: JsonPropertyName("genres")] List<string> Genres,
        // (track-id)
        [property: JsonPropertyName("id")] string Id
        );
}