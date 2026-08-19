namespace Words.Core;

/// <summary>
/// What someone wants to know about an answer once they have it.
/// </summary>
/// <remarks>
/// The lexicon holds display forms, search keys and scores and nothing else — no
/// definitions and no thesaurus — so a lookup is always a question put to somebody else.
/// See <see cref="SearchEngine"/>.
/// </remarks>
public enum LookupKind
{
    /// <summary>What it means.</summary>
    Definition,

    /// <summary>Other words that mean the same.</summary>
    Synonyms,
}
