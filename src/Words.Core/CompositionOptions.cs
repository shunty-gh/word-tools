namespace Words.Core;

/// <summary>
/// Bounds on answers assembled from several entries.
/// </summary>
/// <remarks>
/// Composition is opt-in because it is a different question from an exact anagram, and
/// because it needs bounding in ways an ordinary anagram does not. The search itself is
/// cheap; the number of valid splits is what needs containing.
/// </remarks>
public sealed record CompositionOptions
{
    /// <summary>Two components, minimum length three — the defaults.</summary>
    public static CompositionOptions Default { get; } = new();

    /// <summary>Beyond three components an answer stops being recognisable as one.</summary>
    public const int MaxComponentsCeiling = 3;

    /// <summary>
    /// One-letter components are never allowed: they would pad every answer with stray
    /// A's and I's for no gain.
    /// </summary>
    public const int MinComponentLengthFloor = 2;

    /// <summary>
    /// Blanks multiply the whole search, so composition allows fewer of them than a plain
    /// anagram does.
    /// </summary>
    public const int MaxBlanks = 1;

    /// <summary>How many entries an answer may be assembled from, 2 to 3.</summary>
    public int MaxComponents { get; init; } = 2;

    /// <summary>The shortest an individual component may be, at least 2.</summary>
    public int MinComponentLength { get; init; } = 3;

    /// <summary>
    /// Whether an entry may be used as a component. Components are drawn only from ordinary
    /// single words: composing out of phrases or proper nouns produces answers like
    /// "RED HERRING A", which are not answers to anything.
    /// </summary>
    public static bool IsEligibleComponent(Entry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return entry.Kinds.HasFlag(EntryKinds.SingleWord)
            && !entry.Kinds.HasFlag(EntryKinds.ProperNoun);
    }

    internal void Validate()
    {
        if (MaxComponents is < 2 or > MaxComponentsCeiling)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxComponents),
                MaxComponents,
                $"A composition must have between 2 and {MaxComponentsCeiling} components.");
        }

        if (MinComponentLength < MinComponentLengthFloor)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MinComponentLength),
                MinComponentLength,
                $"A component must be at least {MinComponentLengthFloor} letters.");
        }
    }
}
