namespace Words.Core;

/// <summary>
/// One contributor of entries to the lexicon.
/// </summary>
/// <remarks>
/// The lexicon is built from an <em>ordered collection</em> of these, not from a single
/// artefact — see <see href="../../docs/adr/0006-lexicon-loads-from-ordered-sources.md">ADR
/// 0006</see>. Today that collection holds the built-in artefact followed by the user's
/// personal additions; a sync API would one day join it without anything else changing.
/// Do not collapse it to a single source because it currently holds only two.
/// </remarks>
public interface ILexiconSource
{
    /// <summary>Name used in diagnostics and in the manifest.</summary>
    string Name { get; }

    /// <summary>
    /// Loads every entry this source contributes. Asynchronous because a source may one
    /// day be a network call; local sources complete synchronously.
    /// </summary>
    /// <remarks>
    /// A source must not return two entries with the same display form — deduplicating
    /// within a source is its own responsibility, since it knows its format and is best
    /// placed to do it cheaply. <see cref="Lexicon"/> deduplicates only *between* sources.
    /// </remarks>
    ValueTask<IReadOnlyList<Entry>> LoadAsync(CancellationToken cancellationToken = default);
}
