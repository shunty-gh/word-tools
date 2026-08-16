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
    private const int FieldCount = 4;

    public static async Task WriteAsync(
        Stream destination,
        IReadOnlyCollection<Entry> entries,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(entries);

        await using var gzip = new GZipStream(destination, CompressionLevel.Optimal, leaveOpen: true);
        await using var writer = new StreamWriter(gzip, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        // The count travels in the header purely so the reader can size its list once
        // instead of growing and copying it a score of times.
        await writer.WriteLineAsync($"{FormatHeader}\t{entries.Count}".AsMemory(), cancellationToken)
            .ConfigureAwait(false);

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

    /// <summary>
    /// Reads every entry from an artefact stream.
    /// </summary>
    /// <remarks>
    /// Decompresses into memory in one asynchronous copy, then parses synchronously. Half a
    /// million <c>ReadLineAsync</c> calls cost more in state machines than the parsing
    /// itself, and buffering keeps the only blocking work off the wire — which matters for
    /// a single-threaded WebAssembly host, where the async copy yields but a per-line await
    /// loop would merely be slow.
    /// </remarks>
    public static async Task<IReadOnlyList<Entry>> ReadAsync(
        Stream source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        using var buffer = new MemoryStream();

        await using (var gzip = new GZipStream(source, CompressionMode.Decompress, leaveOpen: true))
        {
            await gzip.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        }

        buffer.Position = 0;
        return Parse(buffer, cancellationToken);
    }

    private static List<Entry> Parse(Stream decompressed, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(decompressed, Encoding.UTF8);

        var header = reader.ReadLine();
        var entries = new List<Entry>(ParseHeader(header));

        Span<Range> fields = stackalloc Range[FieldCount + 1];

        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0)
            {
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Split into ranges rather than strings: the display form is the only field
            // that needs allocating, and this runs half a million times per load.
            var span = line.AsSpan();

            if (span.Split(fields, FieldSeparator) != FieldCount)
            {
                throw new InvalidDataException($"Malformed artefact line: '{line}'.");
            }

            entries.Add(Entry.Create(
                new string(span[fields[0]]),
                int.Parse(span[fields[1]], CultureInfo.InvariantCulture),
                (Sources)int.Parse(span[fields[2]], CultureInfo.InvariantCulture),
                span[fields[3]] is "1"));
        }

        return entries;
    }

    /// <summary>Validates the header and returns the entry count it advertises, if any.</summary>
    private static int ParseHeader(string? header)
    {
        var span = (header ?? string.Empty).AsSpan();
        var separator = span.IndexOf(FieldSeparator);
        var format = separator < 0 ? span : span[..separator];

        if (!format.SequenceEqual(FormatHeader))
        {
            throw new InvalidDataException(
                $"Expected lexicon artefact header '{FormatHeader}' but found '{header ?? "<empty>"}'.");
        }

        return separator >= 0 && int.TryParse(span[(separator + 1)..], CultureInfo.InvariantCulture, out var count)
            ? count
            : 0;
    }
}
