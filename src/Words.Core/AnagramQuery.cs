namespace Words.Core;

/// <summary>
/// A question about which answers use exactly these letters.
/// </summary>
/// <remarks>
/// An exact anagram: every letter given is used, and so is every blank, so an answer's
/// length is always the letters supplied plus the blanks. There is no "words you could make
/// from some of these letters" reading — that is composition, and it arrives in phase 5.
/// </remarks>
public sealed record AnagramQuery
{
    /// <summary>
    /// The letters, with <c>?</c> or <c>.</c> for each one still unknown. Case, accents,
    /// spaces, hyphens and apostrophes are all forgiven.
    /// </summary>
    public required string Letters { get; init; }

    /// <summary>Which entries are eligible. Excludes racy entries by default.</summary>
    public EntryFilter Filter { get; init; } = EntryFilter.Default;
}
