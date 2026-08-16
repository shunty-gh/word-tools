namespace Words.Core;

/// <summary>
/// The complete body of entries the solver can return, with the indexes the two query
/// kinds need.
/// </summary>
/// <remarks>
/// Immutable once loaded, and safe to share across threads and queries.
/// <para>
/// Two indexes, one per query kind. Patterns fix an answer's length exactly, so they only
/// ever scan entries of that length. Anagrams match on letters regardless of order, so
/// they look up the canonical form — the search key with its letters sorted. Both are
/// one-to-many: distinct entries frequently share a search key, and far more share a
/// canonical form.
/// </para>
/// <para>
/// Both are built lazily. A pattern query never touches the anagram index, and building it
/// eagerly costs a canonical form — a sort and a string allocation — for every one of half
/// a million entries. In a command-line tool that pays its startup cost on every single
/// invocation, that is the difference between a query feeling instant and feeling slow.
/// </para>
/// </remarks>
public sealed class Lexicon
{
    private static readonly Entry[] None = [];

    private readonly Lazy<Dictionary<int, Entry[]>> _byLength;
    private readonly Lazy<Dictionary<string, Entry[]>> _byCanonicalForm;

    /// <summary>
    /// Groups entries by a key. Written by hand rather than with <c>GroupBy</c>: over half a
    /// million entries, LINQ's intermediate groupings cost noticeably more than accumulating
    /// into lists directly.
    /// </summary>
    private static Dictionary<TKey, Entry[]> BuildIndex<TKey>(
        IReadOnlyList<Entry> entries,
        Func<Entry, TKey> keySelector,
        IEqualityComparer<TKey>? comparer)
        where TKey : notnull
    {
        var buckets = new Dictionary<TKey, List<Entry>>(comparer);

        foreach (var entry in entries)
        {
            var key = keySelector(entry);

            if (!buckets.TryGetValue(key, out var bucket))
            {
                buckets[key] = bucket = [];
            }

            bucket.Add(entry);
        }

        var index = new Dictionary<TKey, Entry[]>(buckets.Count, comparer);

        foreach (var (key, bucket) in buckets)
        {
            index[key] = [.. bucket];
        }

        return index;
    }

    private static Dictionary<string, int> BuildPositionIndex(List<Entry> entries)
    {
        var positions = new Dictionary<string, int>(entries.Count, StringComparer.Ordinal);

        for (var i = 0; i < entries.Count; i++)
        {
            positions[entries[i].DisplayForm] = i;
        }

        return positions;
    }

    private Lexicon(IReadOnlyList<Entry> entries, IReadOnlyList<string> sourceNames)
    {
        Entries = entries;
        SourceNames = sourceNames;

        _byLength = new Lazy<Dictionary<int, Entry[]>>(
            () => BuildIndex(entries, e => e.SearchKey.Length, comparer: null));

        _byCanonicalForm = new Lazy<Dictionary<string, Entry[]>>(
            () => BuildIndex(entries, e => SearchKeys.ToCanonical(e.SearchKey), StringComparer.Ordinal));
    }

    /// <summary>
    /// Every entry, in load order — the built-in artefact's own order first, then anything
    /// later sources added. Deliberately not re-sorted here: sorting half a million entries
    /// costs real time at startup, and results are ordered at presentation anyway.
    /// </summary>
    public IReadOnlyList<Entry> Entries { get; }

    /// <summary>Names of the sources that contributed, in load order.</summary>
    public IReadOnlyList<string> SourceNames { get; }

    public int Count => Entries.Count;

    /// <summary>How many distinct search-key lengths are present. Builds the length index.</summary>
    public int DistinctLengths => _byLength.Value.Count;

    /// <summary>How many distinct canonical forms are present. Builds the anagram index.</summary>
    public int DistinctCanonicalForms => _byCanonicalForm.Value.Count;

    /// <summary>Every entry whose search key is exactly <paramref name="length"/> letters.</summary>
    public IReadOnlyList<Entry> OfLength(int length) =>
        _byLength.Value.TryGetValue(length, out var entries) ? entries : None;

    /// <summary>
    /// Every entry that is an anagram of the given canonical form. Pass a key through
    /// <see cref="SearchKeys.ToCanonical"/> first.
    /// </summary>
    public IReadOnlyList<Entry> WithCanonicalForm(string canonicalForm) =>
        _byCanonicalForm.Value.TryGetValue(canonicalForm, out var entries) ? entries : None;

    /// <summary>
    /// Loads and merges every source, in order.
    /// </summary>
    /// <remarks>
    /// Merging is keyed on display form: identical forms combine their provenance and take
    /// the most generous score, while distinct forms sharing a search key are all kept,
    /// because <c>Polish</c> and <c>polish</c> are different answers.
    /// </remarks>
    public static async ValueTask<Lexicon> LoadAsync(
        IEnumerable<ILexiconSource> sources,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sources);

        var entries = new List<Entry>();
        var names = new List<string>();

        // Display form to position, built only once a second source actually contributes.
        // With just the built-in artefact — overwhelmingly the common case — that is half a
        // million dictionary inserts avoided for no benefit.
        Dictionary<string, int>? positions = null;

        foreach (var source in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            names.Add(source.Name);

            var loaded = await source.LoadAsync(cancellationToken).ConfigureAwait(false);
            if (loaded.Count == 0)
            {
                continue;
            }

            if (entries.Count == 0)
            {
                entries.AddRange(loaded);
                continue;
            }

            positions ??= BuildPositionIndex(entries);

            foreach (var entry in loaded)
            {
                if (positions.TryGetValue(entry.DisplayForm, out var position))
                {
                    entries[position] = entries[position]
                        .CombineWith(entry.Score, entry.Sources, entry.IsRacy);
                }
                else
                {
                    positions[entry.DisplayForm] = entries.Count;
                    entries.Add(entry);
                }
            }
        }

        return new Lexicon(entries, names);
    }
}
