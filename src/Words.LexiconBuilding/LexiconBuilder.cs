using System.Security.Cryptography;
using Words.Core;

namespace Words.LexiconBuilding;

public sealed record LexiconBuildResult(IReadOnlyList<Entry> Entries, LexiconManifest Manifest);

/// <summary>
/// Merges every word list in a directory into one lexicon.
/// </summary>
public static class LexiconBuilder
{
    /// <summary>How many opening lines are shown to readers when identifying a file.</summary>
    private const int SniffLineCount = 5;

    public static LexiconBuildResult Build(
        string sourceDirectory,
        IEnumerable<IWordListReader>? readers = null,
        Action<string>? log = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);

        var available = (readers ?? DefaultReaders()).ToList();
        var merged = new Dictionary<string, Entry>(StringComparer.Ordinal);
        var sources = new List<LexiconSourceInfo>();
        var discarded = 0;

        // Ordered so a rebuild from the same inputs produces the same artefact.
        var files = Directory.GetFiles(sourceDirectory, "*.txt").OrderBy(f => f, StringComparer.Ordinal);

        foreach (var file in files)
        {
            var reader = Identify(file, available);
            if (reader is null)
            {
                log?.Invoke($"  skipped {Path.GetFileName(file)} (not a recognised word list)");
                continue;
            }

            var read = 0;

            using (var text = new StreamReader(file))
            {
                foreach (var raw in reader.Read(text))
                {
                    read++;

                    if (SearchKeys.From(raw.DisplayForm).Length == 0)
                    {
                        discarded++;
                        continue;
                    }

                    Merge(merged, raw);
                }
            }

            sources.Add(new LexiconSourceInfo(reader.Name, Path.GetFileName(file), Sha256Of(file), read));
            log?.Invoke($"  {reader.Name,-8} {Path.GetFileName(file),-45} {read,9:N0} entries");
        }

        var entries = merged.Values
            .OrderBy(e => e.DisplayForm, StringComparer.Ordinal)
            .ToList();

        var manifest = new LexiconManifest(
            sources,
            entries.Count,
            entries.Count(e => e.Kinds.HasFlag(EntryKinds.SingleWord)),
            entries.Count(e => e.Kinds.HasFlag(EntryKinds.Phrase)),
            entries.Count(e => e.Kinds.HasFlag(EntryKinds.ProperNoun)),
            entries.Count(e => e.IsRacy),
            discarded);

        return new LexiconBuildResult(entries, manifest);
    }

    public static IReadOnlyList<IWordListReader> DefaultReaders() =>
        [new EsdbWordListReader(), new NedigerWordListReader()];

    /// <summary>
    /// Merges one raw entry into the accumulating lexicon, keyed on the display form.
    /// </summary>
    /// <remarks>
    /// Keyed on display form rather than search key, because distinct display forms sharing
    /// a search key are usually genuinely different answers — <c>Polish</c> and
    /// <c>polish</c> are not the same word, and both are legitimate. Collapsing them would
    /// silently lose one. Entries that *are* identical merge their provenance, take the
    /// most generous score, and stay racy if any source thought so.
    /// </remarks>
    private static void Merge(Dictionary<string, Entry> merged, RawEntry raw)
    {
        if (merged.TryGetValue(raw.DisplayForm, out var existing))
        {
            merged[raw.DisplayForm] = existing.CombineWith(raw.Score, raw.Source, raw.IsRacy);
            return;
        }

        merged[raw.DisplayForm] = Entry.Create(raw.DisplayForm, raw.Score, raw.Source, raw.IsRacy);
    }

    private static IWordListReader? Identify(string file, IEnumerable<IWordListReader> readers)
    {
        var sample = new List<string>(SniffLineCount);

        using (var text = new StreamReader(file))
        {
            while (sample.Count < SniffLineCount && text.ReadLine() is { } line)
            {
                if (line.Trim().Length > 0)
                {
                    sample.Add(line);
                }
            }
        }

        return sample.Count == 0 ? null : readers.FirstOrDefault(r => r.CanRead(sample));
    }

    private static string Sha256Of(string file)
    {
        using var stream = File.OpenRead(file);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }
}
