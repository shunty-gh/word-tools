using Words.Core;

namespace Words.Cli;

/// <summary>
/// Decides where the lexicon's entries come from. The engine never makes this decision —
/// it is handed an ordered collection of sources and merges them.
/// </summary>
internal static class Composition
{
    /// <summary>Overrides the personal words file, for testing or a second profile.</summary>
    private const string PersonalPathVariable = "WORDS_PERSONAL";

    public static string PersonalWordsPath =>
        Environment.GetEnvironmentVariable(PersonalPathVariable) is { Length: > 0 } configured
            ? configured
            : FilePersonalWordStore.DefaultPath;

    /// <summary>
    /// The sources, in merge order: the built-in artefact first, then personal additions,
    /// so a personal entry can raise the score of a word already in the lexicon.
    /// </summary>
    public static IReadOnlyList<ILexiconSource> Sources() =>
    [
        EmbeddedLexicon.Source,
        new PersonalLexiconSource(new FilePersonalWordStore(PersonalWordsPath)),
    ];

    public static ValueTask<Lexicon> LoadLexiconAsync(CancellationToken cancellationToken = default) =>
        Lexicon.LoadAsync(Sources(), cancellationToken);
}
