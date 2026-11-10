using System.Text.Json;
using System.Text.Json.Serialization;

namespace back_end.Configurations;

// How the API writes and reads JSON. Program.cs and DtoJsonKeysTests both use this,
// so the test checks the keys the app really sends
public static class ApiJsonOptions
{
    public static void Apply(JsonSerializerOptions options)
    {
        options.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.Converters.Add(new JsonStringEnumConverter());
    }
}
