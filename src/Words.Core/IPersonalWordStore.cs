namespace Words.Core;

/// <summary>
/// The user's own entries, which they can add to as they go.
/// </summary>
/// <remarks>
/// An abstraction rather than direct file access so that a synced remote store can replace
/// a local file without <c>Words.Core</c> changing — see
/// <see href="../../docs/adr/0006-lexicon-loads-from-ordered-sources.md">ADR 0006</see>.
/// </remarks>
public interface IPersonalWordStore
{
    /// <summary>
    /// Every stored line, verbatim. Parsing — comments, blank lines, optional scores — is
    /// the reader's job, so the store stays a dumb list of lines.
    /// </summary>
    ValueTask<IReadOnlyList<string>> ReadLinesAsync(CancellationToken cancellationToken = default);

    /// <summary>Appends one entry.</summary>
    ValueTask AddAsync(string displayForm, CancellationToken cancellationToken = default);
}
