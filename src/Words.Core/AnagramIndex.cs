namespace Words.Core;

/// <summary>
/// Entries grouped by canonical form — the search key with its letters sorted — so an
/// anagram query is a lookup rather than a scan.
/// </summary>
/// <remarks>
/// Stored as one array of entries sorted by canonical form, plus the start of each group,
/// rather than as a dictionary of arrays. The dictionary version cost 77 MB on the real
/// lexicon, most of it not the keys but the <em>437,476 separate <c>Entry[]</c> arrays</em>
/// it held — the great majority holding a single entry, each carrying an object header — and
/// the <c>List&lt;Entry&gt;</c> used to build every one of them.
/// <para>
/// The canonical forms are concatenated into one <c>char</c> buffer and found by binary
/// search. A packed letter-count key would be smaller still, but the lexicon contains an
/// entry with sixteen of one letter ("Buffalo buffalo Buffalo buffalo …"), so any fixed
/// width risks two different letter-sets colliding — which would produce wrong answers
/// rather than slow ones.
/// </para>
/// </remarks>
internal sealed class AnagramIndex
{
    private static readonly Entry[] NoEntries = [];

    /// <summary>Every canonical form, concatenated in sorted order.</summary>
    private readonly char[] _forms;

    /// <summary>Where each group's canonical form starts in <see cref="_forms"/>, plus a tail.</summary>
    private readonly int[] _formStarts;

    /// <summary>Where each group's entries start in <see cref="_entries"/>, plus a tail.</summary>
    private readonly int[] _entryStarts;

    /// <summary>Every entry, ordered by canonical form so a group is a contiguous run.</summary>
    private readonly Entry[] _entries;

    private AnagramIndex(char[] forms, int[] formStarts, int[] entryStarts, Entry[] entries)
    {
        _forms = forms;
        _formStarts = formStarts;
        _entryStarts = entryStarts;
        _entries = entries;
    }

    /// <summary>How many distinct canonical forms the lexicon holds.</summary>
    public int GroupCount => _formStarts.Length - 1;

    public static AnagramIndex Build(IReadOnlyList<Entry> entries)
    {
        // Computed once and kept only for the sort: recomputing inside the comparison would
        // cost a canonical form per comparison, which is O(n log n) of them.
        var forms = new string[entries.Count];

        for (var i = 0; i < entries.Count; i++)
        {
            forms[i] = SearchKeys.ToCanonical(entries[i].SearchKey);
        }

        var order = new int[entries.Count];

        for (var i = 0; i < order.Length; i++)
        {
            order[i] = i;
        }

        Array.Sort(order, (left, right) => string.CompareOrdinal(forms[left], forms[right]));

        var sorted = new Entry[entries.Count];
        var groups = 0;
        var characters = 0;

        for (var i = 0; i < order.Length; i++)
        {
            sorted[i] = entries[order[i]];

            if (i == 0 || !string.Equals(forms[order[i]], forms[order[i - 1]], StringComparison.Ordinal))
            {
                groups++;
                characters += forms[order[i]].Length;
            }
        }

        var formBuffer = new char[characters];
        var formStarts = new int[groups + 1];
        var entryStarts = new int[groups + 1];

        var group = 0;
        var written = 0;

        for (var i = 0; i < order.Length; i++)
        {
            if (i != 0 && string.Equals(forms[order[i]], forms[order[i - 1]], StringComparison.Ordinal))
            {
                continue;
            }

            var form = forms[order[i]];
            formStarts[group] = written;
            entryStarts[group] = i;

            form.CopyTo(0, formBuffer, written, form.Length);
            written += form.Length;
            group++;
        }

        formStarts[groups] = written;
        entryStarts[groups] = entries.Count;

        return new AnagramIndex(formBuffer, formStarts, entryStarts, sorted);
    }

    /// <summary>
    /// Every entry whose letters match, or an empty list. The canonical form must already be
    /// sorted — see <see cref="SearchKeys.ToCanonical"/>.
    /// </summary>
    public IReadOnlyList<Entry> Lookup(string canonicalForm)
    {
        ArgumentNullException.ThrowIfNull(canonicalForm);

        var group = Find(canonicalForm);

        if (group < 0)
        {
            return NoEntries;
        }

        var start = _entryStarts[group];

        // An ArraySegment is a struct implementing IReadOnlyList, so this slices without
        // copying the entries out.
        return new ArraySegment<Entry>(_entries, start, _entryStarts[group + 1] - start);
    }

    /// <summary>Binary search for the group holding this canonical form, or -1.</summary>
    private int Find(ReadOnlySpan<char> canonicalForm)
    {
        var low = 0;
        var high = GroupCount - 1;

        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            var comparison = FormAt(middle).SequenceCompareTo(canonicalForm);

            if (comparison == 0)
            {
                return middle;
            }

            if (comparison < 0)
            {
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return -1;
    }

    private ReadOnlySpan<char> FormAt(int group) =>
        _forms.AsSpan(_formStarts[group], _formStarts[group + 1] - _formStarts[group]);
}
