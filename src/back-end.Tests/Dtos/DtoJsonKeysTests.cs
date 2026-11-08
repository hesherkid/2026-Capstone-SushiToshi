using Xunit;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using back_end.Dtos;

namespace back_end.Tests.Dtos;

public class DtoJsonKeysTests
{
    /// <summary>
    /// Checks the JSON keys every DTO sends and accepts against DtoJsonKeys.txt.
    /// Renaming a C# property can quietly change its JSON key, and the front end then reads undefined
    /// instead of failing, so a key change has to show up here first.
    /// When a key change is intended, regenerate the file and commit it with the change:
    ///     UPDATE_DTO_JSON_KEYS=1 dotnet test --filter DtoJsonKeysTests
    /// </summary>

    // The same options as AddJsonOptions in Program.cs, on top of ASP.NET's web defaults
    private static readonly JsonSerializerOptions ApiOptions = new(JsonSerializerDefaults.Web)
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public void DtoJsonKeys_MatchBaseline()
    {
        var current = CurrentKeys();
        var baselinePath = BaselinePath();

        if (Environment.GetEnvironmentVariable("UPDATE_DTO_JSON_KEYS") == "1")
        {
            File.WriteAllLines(baselinePath, current.Select(t => $"{t.Key}: {string.Join(", ", t.Value)}"));
            return;
        }

        var baseline = File.ReadAllLines(baselinePath)
            .Where(line => line.Contains(": "))
            .ToDictionary(line => line[..line.IndexOf(": ")], line => line[(line.IndexOf(": ") + 2)..].Split(", ", StringSplitOptions.RemoveEmptyEntries).ToList());

        var differences = new List<string>();
        foreach (var type in baseline.Keys.Union(current.Keys).Order())
        {
            if (!current.TryGetValue(type, out var now)) { differences.Add($"{type}: DTO removed"); continue; }
            if (!baseline.TryGetValue(type, out var was)) { differences.Add($"{type}: new DTO with keys {string.Join(", ", now)}"); continue; }

            var removed = was.Except(now).ToList();
            var added = now.Except(was).ToList();
            if (removed.Count > 0 || added.Count > 0)
                differences.Add($"{type}: removed [{string.Join(", ", removed)}], added [{string.Join(", ", added)}]");
        }

        Assert.True(differences.Count == 0,
            "DTO JSON keys changed. If this is intended, regenerate DtoJsonKeys.txt (see the summary at the top of DtoJsonKeysTests).\n" +
            string.Join("\n", differences));
    }

    // Every DTO class mapped to its JSON keys, sorted so the file only changes when a key does
    private static SortedDictionary<string, List<string>> CurrentKeys()
    {
        var types = typeof(LocationResponseDto).Assembly.GetTypes()
            .Where(t => t.Namespace?.StartsWith("back_end.Dtos") == true)
            .Where(t => t.IsClass && !t.IsGenericTypeDefinition && !(t.IsAbstract && t.IsSealed))
            .Where(t => !t.Name.Contains('<') && !typeof(JsonConverter).IsAssignableFrom(t));

        var keys = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var type in types)
        {
            keys[type.FullName!] = ApiOptions.GetTypeInfo(type).Properties
                .Select(p => p.Name)
                .Order(StringComparer.Ordinal)
                .ToList();
        }
        return keys;
    }

    // DtoJsonKeys.txt sits next to this file, so it's found from the source folder, not the build output
    private static string BaselinePath([CallerFilePath] string testFile = "") =>
        Path.Combine(Path.GetDirectoryName(testFile)!, "DtoJsonKeys.txt");
}
