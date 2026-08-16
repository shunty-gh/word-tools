using System.Globalization;

namespace Words.Core;

/// <summary>
/// The user's personal additions, as a lexicon source.
/// </summary>
/// <remarks>
/// The file is meant to be hand-edited, so it tolerates blank lines and <c>#</c> comments,
/// and a score is optional. Entries default to <see cref="DefaultScore"/> — high, because
/// the user added them deliberately and presumably wants to see them.
/// </remarks>
public sealed class PersonalLexiconSource(IPersonalWordStore store) : ILexiconSource
{
    /// <summary>Score given to a personal entry that does not state one.</summary>
    public const int DefaultScore = 90;

    private readonly IPersonalWordStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public string Name => "personal";

    public async ValueTask<IReadOnlyList<Entry>> LoadAsync(CancellationToken cancellationToken = default)
    {
        var lines = await _store.ReadLinesAsync(cancellationToken).ConfigureAwait(false);

        // Deduplicated here rather than by the Lexicon: a hand-edited file will accumulate
        // repeats, and a source owes its consumer a clean list. The later line wins, so
        // re-adding a word with a different score updates it.
        var entries = new Dictionary<string, Entry>(StringComparer.Ordinal);

        foreach (var line in lines)
        {
            if (TryParse(line, out var entry))
            {
                entries[entry.DisplayForm] = entry;
            }
        }

        return [.. entries.Values];
    }

    private static bool TryParse(string line, out Entry entry)
    {
        entry = null!;

        var trimmed = line.Trim();
        if (trimmed.Length == 0 || trimmed.StartsWith('#'))
        {
            return false;
        }

        var displayForm = trimmed;
        var score = DefaultScore;

        var separator = trimmed.LastIndexOf(';');
        if (separator > 0
            && int.TryParse(
                trimmed.AsSpan(separator + 1).Trim(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var stated))
        {
            displayForm = trimmed[..separator].Trim();
            score = Math.Clamp(stated, 0, 100);
        }

        if (displayForm.Length == 0 || SearchKeys.From(displayForm).Length == 0)
        {
            return false;
        }

        entry = Entry.Create(displayForm, score, Sources.Personal);
        return true;
    }
}
