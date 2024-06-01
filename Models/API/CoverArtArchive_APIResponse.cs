using System.Text.Json.Serialization;

namespace AudioSnapServer.Models;

public record class CoverArtArchive_APIResponse(
    [property: JsonPropertyName("images")] List<CoverArtArchive_APIResponse.Image> Images
)
{
    public record class Image(
        [property: JsonPropertyName("types")] List<string> Types,
        [property: JsonPropertyName("image")] Uri OriginalImage,
        [property: JsonPropertyName("thumbnails")] ThumbnailLinks Thumbnails
    );

    public record class ThumbnailLinks(
        [property: JsonPropertyName("250")] Uri Link250px,
        [property: JsonPropertyName("500")] Uri Link500px,
        [property: JsonPropertyName("1200")] Uri Link1200px,
        [property: JsonPropertyName("small")] Uri LinkSmall,
        [property: JsonPropertyName("large")] Uri LinkLarge
    );
}