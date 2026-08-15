namespace Words.Core;

/// <summary>
/// The word list, or lists, an entry came from. Retained on every entry so results can be
/// filtered by provenance without rebuilding the lexicon.
/// </summary>
[Flags]
public enum Sources
{
    None = 0,

    /// <summary>English Speller Database, formerly SCOWL.</summary>
    Esdb = 1,

    /// <summary>The Nediger crossword list.</summary>
    Nediger = 2,

    /// <summary>Words the user added themselves.</summary>
    Personal = 4,
}
