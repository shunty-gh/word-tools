using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace Words.Core;

/// <summary>
/// Reads and writes the built lexicon artefact: a gzip-compressed, tab-separated list of
/// entries, sorted by display form.
/// </summary>
/// <remarks>
/// Deliberately stream-based rather than path-based. <c>Words.Core</c> must not acquire a
/// file-system dependency — the host opens the stream, whether that is a file, an embedded
/// resource, or one day an HTTP response.
/// <para>
/// Only the display form, score, sources and racy flag are stored. The search key and
/// kinds are derived on load by <see cref="Entry.Create"/>, which keeps the artefact small
/// and guarantees the builder and the engine can never disagree about how a display form
/// normalises.
/// </para>
/// </remarks>
public static class LexiconArtefact
{
    private const string FormatHeader = "words-lexicon/1";
    private const char FieldSeparator = '\t';

    public static async Task WriteAsync(
        Stream destination,
        IEnumerable<Entry> entries,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(entries);

        await using var gzip = new GZipStream(destination, CompressionLevel.Optimal, leaveOpen: true);
        await using var writer = new StreamWriter(gzip, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        await writer.WriteLineAsync(FormatHeader.AsMemory(), cancellationToken).ConfigureAwait(false);

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (entry.DisplayForm.Contains(FieldSeparator, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Display form '{entry.DisplayForm}' contains a tab, which the artefact format uses as its separator.");
            }

            var line = string.Create(
                CultureInfo.InvariantCulture,
                $"{entry.DisplayForm}\t{entry.Score}\t{(int)entry.Sources}\t{(entry.IsRacy ? 1 : 0)}");

            await writer.WriteLineAsync(line.AsMemory(), cancellationToken).ConfigureAwait(false);
        }
    }

    public static async Task<IReadOnlyList<Entry>> ReadAsync(
        Stream source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        await using var gzip = new GZipStream(source, CompressionMode.Decompress, leaveOpen: true);
        using var reader = new StreamReader(gzip, Encoding.UTF8);

        var header = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        if (header != FormatHeader)
        {
            throw new InvalidDataException(
                $"Expected lexicon artefact header '{FormatHeader}' but found '{header ?? "<empty>"}'.");
        }

        var entries = new List<Entry>();

        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            if (line.Length == 0)
            {
                continue;
            }

            var fields = line.Split(FieldSeparator);
            if (fields.Length != 4)
            {
                throw new InvalidDataException($"Malformed artefact line: '{line}'.");
            }

            entries.Add(Entry.Create(
                fields[0],
                int.Parse(fields[1], CultureInfo.InvariantCulture),
                (Sources)int.Parse(fields[2], CultureInfo.InvariantCulture),
                fields[3] == "1"));
        }

        return entries;
    }
}
