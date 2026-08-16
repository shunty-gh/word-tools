using System.Text.Json;
using System.Text.Json.Serialization;

namespace Words.LexiconBuilding;

/// <summary>One source file that contributed to the artefact.</summary>
public sealed record LexiconSourceInfo(string Reader, string File, string Sha256, int EntriesRead);

/// <summary>
/// What went into the artefact. Part of the contract rather than a build log: a remote
/// source will one day need this to know whether a cached copy is stale.
/// </summary>
/// <remarks>
/// Deliberately carries no build timestamp. The artefact is committed, so a rebuild from
/// unchanged inputs must produce a byte-identical file — otherwise every rebuild dirties
/// the diff and the commit stops meaning anything. Provenance comes from the per-file
/// SHA-256 instead, which identifies the inputs more precisely than a clock does.
/// </remarks>
public sealed record LexiconManifest(
    IReadOnlyList<LexiconSourceInfo> Sources,
    int EntryCount,
    int SingleWordCount,
    int PhraseCount,
    int ProperNounCount,
    int RacyCount,
    int DiscardedCount)
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public string ToJson() => JsonSerializer.Serialize(this, SerializerOptions);
}
