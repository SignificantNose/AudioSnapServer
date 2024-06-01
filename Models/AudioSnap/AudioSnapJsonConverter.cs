using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace AudioSnapServer.Models;

public class AudioSnapJsonConverter : JsonConverter<AudioSnap>
{
    public override void Write(Utf8JsonWriter writer, AudioSnap value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        // 1. "properties" : {...}
        writer.WritePropertyName("properties");
        writer.WriteStartObject();
        foreach (string property in value.ValidProperties)
        {
            // test tool; to catch exceptions
            // try
            // {
            writer.WritePropertyName(property);
            JsonSerializer.Serialize(writer, AudioSnap.PropertyMappings[property].GetMappedValue(value));
            // }
            // catch (Exception ex)
            // {
            //     int x = 10;
            //     
            // }
        }
        writer.WriteEndObject();
        
        // 2. ["external-links"] : [...]
        if (value.RESEXTLINKS!=null)
        {
            writer.WritePropertyName("external-links");
            JsonSerializer.Serialize(writer, value.RESEXTLINKS);
        }

        // 3. ["image-link"] : ...
        // not a fan of ToString tbh
        if (value.RESIMGLINK != null) 
        {
            writer.WriteString("image-link", value.RESIMGLINK.ToString());
        }
        
        // 4. ["missing-properties"] : [...]
        if (!(value.MissingProperties.Count < 1))
        {
            writer.WritePropertyName("missing-properties");
            JsonSerializer.Serialize(writer, value.MissingProperties);
        }
        // 5. ["invalid-properties"] : [...]
        if (!(value.InvalidProperties.Count < 1))
        {
            writer.WritePropertyName("invalid-properties");
            JsonSerializer.Serialize(writer, value.InvalidProperties);
        }

        writer.WriteEndObject();
    }

    public override AudioSnap? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }
}