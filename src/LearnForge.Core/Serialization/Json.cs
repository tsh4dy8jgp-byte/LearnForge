using System.Text.Json;
using System.Text.Json.Serialization;

namespace LearnForge.Core;

public static class Json
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 32,
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true
    };

    static Json() => Options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));

    public static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);
    public static T Read<T>(string text) => JsonSerializer.Deserialize<T>(text, Options) ?? throw new JsonException("Empty document.");
}
