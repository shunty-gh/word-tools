namespace Words.Core;

/// <summary>
/// One member of the lexicon — a single thing that could be a puzzle answer. May be a
/// single word, a hyphenated word, or a multi-word phrase.
/// </summary>
/// <param name="DisplayForm">
/// The entry as shown to a person, retaining spaces, hyphens, apostrophes, accents and
/// capitalisation.
/// </param>
/// <param name="SearchKey">
/// The letters a solver types. All matching happens against this; the display form is
/// only ever shown, never searched.
/// </param>
/// <param name="Score">
/// How readily a solver would accept this as an answer, 0–100. Deliberately not word
/// frequency: a crossword list rates entries by how good they are as fill, which is a
/// different judgement from how often a word is written.
/// </param>
public sealed record Entry(
    string DisplayForm,
    string SearchKey,
    EntryKinds Kinds,
    int Score,
    Sources Sources,
    bool IsRacy)
{
    /// <summary>
    /// Creates an entry, deriving its search key and kinds from the display form. Always
    /// prefer this over the constructor so derivation happens in exactly one place.
    /// </summary>
    public static Entry Create(string displayForm, int score, Sources sources, bool isRacy = false)
    {
        ArgumentNullException.ThrowIfNull(displayForm);

        return new Entry(
            displayForm,
            SearchKeys.From(displayForm),
            ClassifyKinds(displayForm),
            score,
            sources,
            isRacy);
    }

    /// <summary>
    /// Combines this entry with another statement of the same display form: take the most
    /// generous score, union the provenance, and stay racy if either said so.
    /// </summary>
    /// <remarks>
    /// Used both when building the artefact and when merging sources at load, so the two
    /// can never drift apart. Merging is keyed on display form, never search key — see
    /// <see href="../../docs/adr/0004-scowl-nediger-lexicon.md">ADR 0004</see>.
    /// </remarks>
    public Entry CombineWith(int score, Sources sources, bool isRacy) => this with
    {
        Score = Math.Max(Score, score),
        Sources = Sources | sources,
        IsRacy = IsRacy || isRacy,
    };

    /// <summary>
    /// A phrase is an entry whose display form contains a space. A proper noun is one
    /// whose first letter is capitalised — word lists carry no metadata, so this is
    /// inferred, and it will misfile acronyms and sentence-cased entries.
    /// </summary>
    private static EntryKinds ClassifyKinds(string displayForm)
    {
        var kinds = displayForm.Contains(' ', StringComparison.Ordinal)
            ? EntryKinds.Phrase
            : EntryKinds.SingleWord;

        foreach (var c in displayForm)
        {
            if (!char.IsLetter(c))
            {
                continue;
            }

            if (char.IsUpper(c))
            {
                kinds |= EntryKinds.ProperNoun;
            }

            break;
        }

        return kinds;
    }
}
