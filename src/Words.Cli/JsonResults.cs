using System.Text.Json;
using System.Text.Json.Serialization;
using Words.Core;

namespace Words.Cli;

/// <summary>One answer, as JSON.</summary>
internal sealed record JsonMatch(
    string Answer,
    IReadOnlyList<string> Words,
    int Score,
    IReadOnlyList<string> Sources,
    bool Racy);

/// <summary>A query's answers, as JSON.</summary>
/// <param name="Total">How many answers were found, before any limit.</param>
/// <param name="Shown">How many are in <paramref name="Matches"/>.</param>
internal sealed record JsonResults(int Total, int Shown, bool Truncated, IReadOnlyList<JsonMatch> Matches);

/// <summary>
/// Source-generated so that JSON keeps working under NativeAOT in phase 8, where
/// reflection-based serialisation does not.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true)]
[JsonSerializable(typeof(JsonResults))]
internal sealed partial class JsonResultsContext : JsonSerializerContext;

internal static class JsonResultsFactory
{
    /// <summary>
    /// The default encoder escapes anything that could be unsafe in HTML, turning
    /// <c>inlet's</c> into <c>inlet's</c> and <c>café</c> into <c>café</c>. This
    /// output goes to a terminal or a file, so it should stay readable.
    /// </summary>
    private static readonly JsonResultsContext Context = new(new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    });

    public static string Serialise(IReadOnlyList<Match> shown, int total)
    {
        var results = new JsonResults(
            total,
            shown.Count,
            shown.Count < total,
            [.. shown.Select(ToJson)]);

        return JsonSerializer.Serialize(results, Context.JsonResults);
    }

    private static JsonMatch ToJson(Match match) => new(
        match.DisplayForm,
        [.. match.Components.Select(c => c.DisplayForm)],
        match.Score,
        [.. NamesOf(match.Components.Aggregate(Core.Sources.None, (all, c) => all | c.Sources))],
        match.Components.Any(c => c.IsRacy));

    private static IEnumerable<string> NamesOf(Sources sources)
    {
        if (sources.HasFlag(Core.Sources.Esdb))
        {
            yield return "esdb";
        }

        if (sources.HasFlag(Core.Sources.Nediger))
        {
            yield return "nediger";
        }

        if (sources.HasFlag(Core.Sources.Personal))
        {
            yield return "personal";
        }
    }
}
