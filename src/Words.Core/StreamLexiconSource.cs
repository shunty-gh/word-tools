namespace Words.Core;

/// <summary>
/// A source backed by a lexicon artefact, supplied as a stream.
/// </summary>
/// <remarks>
/// Takes a factory rather than a stream so the source can be loaded more than once, and so
/// <c>Words.Core</c> never decides where the bytes come from. The caller supplies a file,
/// an embedded resource, or an HTTP response as it sees fit.
/// </remarks>
public sealed class StreamLexiconSource(
    string name,
    Func<CancellationToken, ValueTask<Stream>> openAsync) : ILexiconSource
{
    private readonly Func<CancellationToken, ValueTask<Stream>> _openAsync =
        openAsync ?? throw new ArgumentNullException(nameof(openAsync));

    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));

    public async ValueTask<IReadOnlyList<Entry>> LoadAsync(CancellationToken cancellationToken = default)
    {
        var stream = await _openAsync(cancellationToken).ConfigureAwait(false);

        await using (stream.ConfigureAwait(false))
        {
            return await LexiconArtefact.ReadAsync(stream, cancellationToken).ConfigureAwait(false);
        }
    }
}
