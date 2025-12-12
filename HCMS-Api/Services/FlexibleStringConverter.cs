using System.Text.Json;
using System.Text.Json.Serialization;

namespace HCMS_Api.Services
{
    public class FlexibleStringConverter : JsonConverter<string>
    {
        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // Agar number ho toh usko string mein convert kar do
            if (reader.TokenType == JsonTokenType.Number)
            {
                return reader.GetInt32().ToString();
            }

            // Agar string ho toh directly read kar lo
            if (reader.TokenType == JsonTokenType.String)
            {
                return reader.GetString();
            }


            // Agar null ho toh null return karo
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            throw new JsonException("Unable to convert to string.");
        }

        public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value);
        }
    }
}
