namespace Words.Core;

/// <summary>
/// A question about which answers fit a given shape of letters and gaps.
/// </summary>
/// <remarks>
/// The pattern's length fixes the answer's length exactly, so there is no separate length
/// option — <c>A??D</c> asks for four-letter answers and nothing else.
/// </remarks>
public sealed record PatternQuery
{
    /// <summary>
    /// The pattern: literal letters, <c>?</c> for exactly one unknown letter, and
    /// <c>[abc]</c> / <c>[^def]</c> classes. Case-insensitive.
    /// </summary>
    public required string Pattern { get; init; }

    /// <summary>Which entries are eligible. Excludes racy entries by default.</summary>
    public EntryFilter Filter { get; init; } = EntryFilter.Default;
}
