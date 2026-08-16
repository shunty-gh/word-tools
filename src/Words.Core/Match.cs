namespace Words.Core;

/// <summary>
/// One result for a query: either a single entry, or a composition assembled from several.
/// </summary>
public sealed record Match(IReadOnlyList<Entry> Components)
{
    /// <summary>A match made of one entry.</summary>
    public static Match Of(Entry entry) => new([entry]);

    /// <summary>Whether this match was assembled from more than one entry.</summary>
    public bool IsComposition => Components.Count > 1;

    /// <summary>
    /// How the match reads to a person: the entry's display form, or the components'
    /// display forms separated by spaces.
    /// </summary>
    public string DisplayForm => Components.Count == 1
        ? Components[0].DisplayForm
        : string.Join(' ', Components.Select(c => c.DisplayForm));

    /// <summary>
    /// The match's score, taken from its weakest component — a composition is only as
    /// plausible as the most obscure word in it.
    /// </summary>
    public int Score => Components.Min(c => c.Score);
}
