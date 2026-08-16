using System.Runtime.CompilerServices;

namespace Words.Core;

/// <summary>
/// The solver, over a loaded lexicon.
/// </summary>
public sealed class WordEngine(Lexicon lexicon) : IWordEngine
{
    /// <summary>
    /// How many candidates to scan between yields. Enumeration is CPU-bound, so on a
    /// single-threaded host — a WebAssembly front end — it must hand control back
    /// periodically or the UI freezes for the duration of the search. Large enough that the
    /// yields cost nothing measurable on a desktop.
    /// </summary>
    private const int ScanYieldInterval = 8192;

    /// <summary>
    /// How many index lookups to perform between yields. Far smaller than
    /// <see cref="ScanYieldInterval"/> because an anagram query does at most 3,276 lookups
    /// in total — at the scan interval it would never yield at all.
    /// </summary>
    private const int LookupYieldInterval = 256;

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
            if (++examined % ScanYieldInterval == 0)
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

    public IAsyncEnumerable<Match> QueryAsync(
        AnagramQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        query.Compose?.Validate();

        // Parsed here rather than inside the iterator, for the same reason patterns are
        // compiled early: bad input should be reported at the call.
        var letters = AnagramLetters.Parse(
            query.Letters,
            query.Compose is null ? AnagramLetters.MaxBlanks : CompositionOptions.MaxBlanks);

        return EnumerateAnagramMatches(letters, query, cancellationToken);
    }

    private async IAsyncEnumerable<Match> EnumerateAnagramMatches(
        AnagramLetters letters,
        AnagramQuery query,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var composer = query.Compose is null
            ? null
            : new AnagramComposer(_lexicon, query.Filter, query.Compose);

        // Single-entry answers need no scanning at all: each is an index lookup on the
        // sorted letters, one per combination of blanks.
        var produced = 0;

        foreach (var canonicalForm in letters.CanonicalForms())
        {
            if (++produced % LookupYieldInterval == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
            }

            foreach (var entry in _lexicon.WithCanonicalForm(canonicalForm))
            {
                if (query.Filter.Allows(entry))
                {
                    yield return Match.Of(entry);
                }
            }

            if (composer is null)
            {
                continue;
            }

            foreach (var match in composer.Compose(canonicalForm, cancellationToken))
            {
                if (++produced % LookupYieldInterval == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Yield();
                }

                yield return match;
            }
        }
    }
}
