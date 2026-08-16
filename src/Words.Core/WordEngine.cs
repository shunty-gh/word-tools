using System.Runtime.CompilerServices;

namespace Words.Core;

/// <summary>
/// The solver, over a loaded lexicon.
/// </summary>
public sealed class WordEngine(Lexicon lexicon) : IWordEngine
{
    /// <summary>
    /// How many candidates to examine between yields. Enumeration is CPU-bound, so on a
    /// single-threaded host — a WebAssembly front end — it must hand control back
    /// periodically or the UI freezes for the duration of the search. Large enough that the
    /// yields cost nothing measurable on a desktop.
    /// </summary>
    private const int YieldInterval = 8192;

    private readonly Lexicon _lexicon = lexicon ?? throw new ArgumentNullException(nameof(lexicon));

    public IAsyncEnumerable<Match> QueryAsync(
        PatternQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Compiled here rather than inside the iterator below: an iterator method does not
        // run until it is first enumerated, which would defer a syntax error to a point far
        // from the mistake that caused it.
        var matcher = PatternMatcher.Compile(query.Pattern);

        return EnumeratePatternMatches(matcher, query.Filter, cancellationToken);
    }

    private async IAsyncEnumerable<Match> EnumeratePatternMatches(
        PatternMatcher matcher,
        EntryFilter filter,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // The pattern's length fixes the answer's length, so only one bucket can contain a
        // match — the other half-million entries are never looked at.
        var candidates = _lexicon.OfLength(matcher.Length);
        var examined = 0;

        foreach (var entry in candidates)
        {
            if (++examined % YieldInterval == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
            }

            if (filter.Allows(entry) && matcher.Matches(entry.SearchKey))
            {
                yield return Match.Of(entry);
            }
        }
    }
}
