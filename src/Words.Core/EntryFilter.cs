namespace Words.Core;

/// <summary>
/// Which entries a query is willing to consider, independent of how it matches them.
/// </summary>
/// <remarks>
/// Shared by both query kinds so that "exclude racy entries" or "British sources only"
/// mean exactly the same thing whichever question is being asked.
/// </remarks>
public sealed record EntryFilter
{
    /// <summary>
    /// Everything except racy entries. Phrases and proper nouns are included: a crossword
    /// grid has no spaces, so excluding phrases would simply give wrong answers.
    /// </summary>
    public static EntryFilter Default { get; } = new();

    /// <summary>Which kinds of entry to admit.</summary>
    public EntryKinds Kinds { get; init; } = EntryKinds.All;

    /// <summary>Which sources to admit.</summary>
    public Sources Sources { get; init; } = Sources.All;

    /// <summary>
    /// Whether to admit entries the word list flagged as potentially racy. Off by default:
    /// the apps are headed for stores that review this sort of thing.
    /// </summary>
    public bool IncludeRacy { get; init; }

    public bool Allows(Entry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return (entry.Kinds & Kinds) != EntryKinds.None
            && (entry.Sources & Sources) != Sources.None
            && (IncludeRacy || !entry.IsRacy);
    }
}
