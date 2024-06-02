using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace AudioSnapServer.Models;

public record class AudioSnapClientQuery(
    [property: JsonPropertyName("fingerprint")] [Required(AllowEmptyStrings = false)] string Fingerprint,
    [property: JsonPropertyName("duration")] [PositiveInt] int DurationInSeconds,
    [property: JsonPropertyName("release-properties")] List<string> ReleaseProperties,
    
    [property: JsonPropertyName("matching-rate")] [Range(0.0,1.0)] double MatchingRate = -1,
    
    [property: JsonPropertyName("priorities")] AudioSnapClientQuery.Priorities? QueryPriorities = null,
    [property: JsonPropertyName("cover-size")] int? MaxCoverSize = null,
    [property: JsonPropertyName("cover")] bool IncludeCover = false,
    [property: JsonPropertyName("external-links")] bool IncludeExternalLinks = false
)
{
    public record class Priorities(
        [property: JsonPropertyName("release-format")] Dictionary<string, int>? ReleaseFormat = null,
        [property: JsonPropertyName("release-country")] List<string>? ReleaseCountry = null
        );
}

public class PositiveIntAttribute : ValidationAttribute{
    public string? ErrorMessage { get; set; }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        ValidationResult result = ValidationResult.Success;
        
        if (value == null)
        {
            result = new ValidationResult($"Value of {validationContext.MemberName} must be provided.");
        }
        else
        {
            int acquiredValue = (int)value;
            if (acquiredValue < 1)
            {
                result = new ValidationResult($"Value of {validationContext.MemberName} must be a positive integer");
            }
        }
        return result;
    }
}