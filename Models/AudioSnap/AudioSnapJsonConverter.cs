using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace AudioSnapServer.Models;

public class AudioSnapJsonConverter : JsonConverter<AudioSnap>
{
    private List<string> _propertiesToInclude;
    
    public AudioSnapJsonConverter(List<string> propertiesToInclude)
    {
        _propertiesToInclude = propertiesToInclude;
    }

    public override void Write(Utf8JsonWriter writer, AudioSnap value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        foreach (string property in _propertiesToInclude)
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
    }

    public override AudioSnap? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }
}