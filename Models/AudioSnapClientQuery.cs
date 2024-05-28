﻿using System.Text.Json.Serialization;

namespace AudioSnapServer.Models;

public record class AudioSnapClientQuery(
    [property: JsonPropertyName("fingerprint")] string Fingerprint,
    [property: JsonPropertyName("duration")] int DurationInSeconds,
    [property: JsonPropertyName("priorities")] AudioSnapClientQuery.Priorities QueryPriorities,
    [property: JsonPropertyName("release-properties")] List<string> ReleaseProperties
)
{
    public record class Priorities(
        [property: JsonPropertyName("release-format")] Dictionary<string, double> ReleaseFormat,
        [property: JsonPropertyName("release-country")] List<string> ReleaseCountry
        );
}