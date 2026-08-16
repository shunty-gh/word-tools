namespace Words.Core;

/// <summary>
/// Assembles answers out of several entries whose letters together account for exactly the
/// letters supplied.
/// </summary>
/// <remarks>
/// Each partition is produced once, not once per ordering, by requiring that every
/// component taken contains the lowest letter still unused. Without that rule <c>CAT DOG</c>
/// and <c>DOG CAT</c> are both found, and the duplication compounds with each component.
/// <para>
/// Several entries can share one canonical form, so a single partition may yield several
/// answers — <c>ACT</c> is both <c>act</c> and <c>cat</c>. All of them are returned; they
/// are genuinely different answers.
/// </para>
/// </remarks>
internal sealed class AnagramComposer(Lexicon lexicon, EntryFilter filter, CompositionOptions options)
{
    private readonly Dictionary<string, Entry[]> _eligibleByCanonicalForm = new(StringComparer.Ordinal);

    /// <summary>
    /// Every way of assembling the rack from two or more eligible entries.
    /// </summary>
    /// <param name="rack">The full set of letters, sorted.</param>
    public IEnumerable<Match> Compose(string rack, CancellationToken cancellationToken)
    {
        var counts = new int[26];

        foreach (var letter in rack)
        {
            counts[letter - 'A']++;
        }

        foreach (var components in Partition(counts, rack.Length, options.MaxComponents, cancellationToken))
        {
            // A single component is just an ordinary anagram, which the caller has already
            // reported. Generating and discarding it costs one lookup of the whole rack.
            if (components.Length > 1)
            {
                yield return new Match(components);
            }
        }
    }

    private IEnumerable<Entry[]> Partition(
        int[] counts,
        int remaining,
        int componentsLeft,
        CancellationToken cancellationToken)
    {
        if (remaining == 0)
        {
            yield return [];
            yield break;
        }

        if (componentsLeft == 0)
        {
            yield break;
        }

        if (componentsLeft == 1)
        {
            // Nothing may be left over, so the only candidate is everything that remains.
            foreach (var entry in EligibleFor(CanonicalForm(counts, remaining)))
            {
                yield return [entry];
            }

            yield break;
        }

        var lowest = LowestLetter(counts);

        // Materialised before the loop mutates `counts` underneath it.
        var candidates = SubMultisetsContaining(counts, lowest, options.MinComponentLength, remaining);

        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var rest = remaining - candidate.Length;
            if (rest != 0 && rest < options.MinComponentLength)
            {
                continue;
            }

            var entries = EligibleFor(candidate);
            if (entries.Length == 0)
            {
                continue;
            }

            Take(counts, candidate, -1);

            foreach (var tail in Partition(counts, rest, componentsLeft - 1, cancellationToken))
            {
                foreach (var entry in entries)
                {
                    yield return [entry, .. tail];
                }
            }

            Take(counts, candidate, +1);
        }
    }

    /// <summary>
    /// Every sub-multiset of <paramref name="counts"/> that includes at least one
    /// <paramref name="mustInclude"/>, as canonical forms.
    /// </summary>
    private static List<string> SubMultisetsContaining(
        int[] counts,
        int mustInclude,
        int minSize,
        int maxSize)
    {
        var found = new List<string>();
        var buffer = new char[maxSize];

        Build(0, 0);
        return found;

        void Build(int letter, int length)
        {
            if (letter == 26)
            {
                if (length >= minSize)
                {
                    found.Add(new string(buffer, 0, length));
                }

                return;
            }

            var fewest = letter == mustInclude ? 1 : 0;

            for (var take = fewest; take <= counts[letter] && length + take <= maxSize; take++)
            {
                for (var i = 0; i < take; i++)
                {
                    buffer[length + i] = (char)('A' + letter);
                }

                Build(letter + 1, length + take);
            }
        }
    }

    /// <summary>Entries with this canonical form that may be used as a component.</summary>
    private Entry[] EligibleFor(string canonicalForm)
    {
        if (_eligibleByCanonicalForm.TryGetValue(canonicalForm, out var cached))
        {
            return cached;
        }

        var eligible = lexicon.WithCanonicalForm(canonicalForm)
            .Where(e => filter.Allows(e) && CompositionOptions.IsEligibleComponent(e))
            .ToArray();

        _eligibleByCanonicalForm[canonicalForm] = eligible;
        return eligible;
    }

    private static int LowestLetter(int[] counts)
    {
        for (var letter = 0; letter < counts.Length; letter++)
        {
            if (counts[letter] > 0)
            {
                return letter;
            }
        }

        return -1;
    }

    private static string CanonicalForm(int[] counts, int length)
    {
        var form = new char[length];
        var position = 0;

        for (var letter = 0; letter < counts.Length; letter++)
        {
            for (var i = 0; i < counts[letter]; i++)
            {
                form[position++] = (char)('A' + letter);
            }
        }

        return new string(form);
    }

    private static void Take(int[] counts, string canonicalForm, int direction)
    {
        foreach (var letter in canonicalForm)
        {
            counts[letter - 'A'] += direction;
        }
    }
}
