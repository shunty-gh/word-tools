namespace Words.Core;

/// <summary>How answers are ordered for display.</summary>
public enum SortOrder
{
    /// <summary>Alphabetical, ignoring case. The usual default.</summary>
    Alpha,

    /// <summary>Most likely first.</summary>
    Score,

    /// <summary>Shortest first. Only distinguishes composed answers, which vary in words.</summary>
    Length,
}

/// <summary>
/// Chooses which answers to show, and in what order.
/// </summary>
/// <remarks>
/// Presentation rather than engine, but it lives here so that every front end applies the
/// same rule. The CLI and the app had their own copies, which is precisely how two versions
/// of a subtle rule quietly diverge.
/// </remarks>
public static class MatchOrdering
{
    /// <summary>
    /// Applies the limit and the sort order.
    /// </summary>
    /// <remarks>
    /// When there are more answers than the limit allows, which ones survive is decided by
    /// how likely they are — fewest words first, then the weakest word in the answer — and
    /// only then are the survivors put into the requested order. Truncating in display order
    /// would return every answer beginning with A and nothing else.
    /// </remarks>
    public static IReadOnlyList<Match> Arrange(IReadOnlyList<Match> matches, SortOrder sort, int limit)
    {
        ArgumentNullException.ThrowIfNull(matches);

        IEnumerable<Match> selected = matches;

        if (matches.Count > limit)
        {
            selected = matches
                .OrderBy(m => m.Components.Count)
                .ThenByDescending(m => m.Score)
                .ThenBy(m => m.DisplayForm, StringComparer.OrdinalIgnoreCase)
                .Take(limit);
        }

        return [.. Order(selected, sort)];
    }

    private static IEnumerable<Match> Order(IEnumerable<Match> matches, SortOrder sort) => sort switch
    {
        SortOrder.Score => matches
            .OrderByDescending(m => m.Score)
            .ThenBy(m => m.DisplayForm, StringComparer.OrdinalIgnoreCase),

        SortOrder.Length => matches
            .OrderBy(m => m.DisplayForm.Length)
            .ThenBy(m => m.DisplayForm, StringComparer.OrdinalIgnoreCase),

        _ => matches.OrderBy(m => m.DisplayForm, StringComparer.OrdinalIgnoreCase),
    };
}
