namespace Words.Core;

/// <summary>
/// Answers questions about the lexicon.
/// </summary>
/// <remarks>
/// Results stream rather than arriving as a list — see
/// <see href="../../docs/adr/0002-streaming-query-results.md">ADR 0002</see>. Callers
/// impose their own limits by taking as many as they want and stopping, so no result cap
/// is baked in here.
/// <para>
/// The anagram overload arrives in phase 4.
/// </para>
/// </remarks>
public interface IWordEngine
{
    /// <summary>
    /// Every answer matching the pattern, in lexicon order. Sort at presentation.
    /// </summary>
    /// <exception cref="PatternSyntaxException">
    /// The pattern is malformed. Thrown when the method is called, not when enumeration
    /// begins, so callers do not have to start iterating to discover a typo.
    /// </exception>
    IAsyncEnumerable<Match> QueryAsync(PatternQuery query, CancellationToken cancellationToken = default);
}
