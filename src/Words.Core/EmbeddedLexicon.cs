using System.Reflection;

namespace Words.Core;

/// <summary>
/// The lexicon artefact compiled into this assembly.
/// </summary>
/// <remarks>
/// Embedding it here rather than in each front end means the CLI, MAUI and a web app all
/// get a working lexicon with no deployment step. It is an embedded resource, not a file,
/// so the engine still touches no file system.
/// </remarks>
public static class EmbeddedLexicon
{
    private const string ResourceName = "Words.Core.lexicon.gz";

    /// <summary>A source reading the built-in artefact.</summary>
    public static ILexiconSource Source { get; } = new StreamLexiconSource("built-in", _ => new(Open()));

    /// <summary>Opens the embedded artefact. The caller owns the returned stream.</summary>
    public static Stream Open() =>
        typeof(EmbeddedLexicon).GetTypeInfo().Assembly.GetManifestResourceStream(ResourceName)
        ?? throw new InvalidOperationException(
            $"The lexicon artefact '{ResourceName}' is missing from {typeof(EmbeddedLexicon).Assembly.GetName().Name}. "
            + "Run: dotnet run --project src/Words.Cli -- lexicon build data/sources -o data/lexicon.gz");
}
