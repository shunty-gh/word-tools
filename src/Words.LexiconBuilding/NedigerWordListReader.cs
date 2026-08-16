using System.Globalization;
using Words.Core;

namespace Words.LexiconBuilding;

/// <summary>
/// Reads the Nediger crossword list: one <c>entry;score</c> pair per line.
/// </summary>
/// <remarks>
/// The file is CRLF-terminated and a handful of lines carry trailing whitespace, so both
/// fields are trimmed rather than taken verbatim. Entries themselves may contain
/// semicolons in principle, so the score is split from the *last* separator.
/// </remarks>
public sealed class NedigerWordListReader : IWordListReader
{
    public string Name => "Nediger";

    public bool CanRead(IReadOnlyList<string> sampleLines) =>
        sampleLines.Count > 0 && sampleLines.All(IsEntryLine);

    public IEnumerable<RawEntry> Read(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        while (reader.ReadLine() is { } line)
        {
            if (!TryParse(line, out var displayForm, out var nedigerScore))
            {
                continue;
            }

            yield return new RawEntry(
                displayForm,
                MapScore(nedigerScore),
                Sources.Nediger,
                IsRacy: nedigerScore == RacyScore);
        }
    }

    /// <summary>Nediger's marker for entries "likely too racy for many mainstream venues".</summary>
    private const int RacyScore = 49;

    /// <summary>
    /// Maps Nediger's four scores onto the shared 0–100 scale. Note the author's own
    /// caveat that the 51-versus-99 distinction is "very sporadic and unsystematic" for
    /// long entries, so the gap between them is kept modest rather than treated as
    /// reliable. The racy band says nothing about quality, so it maps to the middle.
    /// </summary>
    private static int MapScore(int nedigerScore) => nedigerScore switch
    {
        99 => 90,
        51 => 60,
        RacyScore => 50,
        25 => 25,
        _ => 50,
    };

    private static bool IsEntryLine(string line) =>
        TryParse(line, out _, out _);

    private static bool TryParse(string line, out string displayForm, out int score)
    {
        displayForm = string.Empty;
        score = 0;

        var trimmed = line.Trim();
        if (trimmed.Length == 0)
        {
            return false;
        }

        var separator = trimmed.LastIndexOf(';');
        if (separator <= 0 || separator == trimmed.Length - 1)
        {
            return false;
        }

        if (!int.TryParse(
                trimmed.AsSpan(separator + 1).Trim(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out score))
        {
            return false;
        }

        displayForm = trimmed[..separator].Trim();
        return displayForm.Length > 0;
    }
}
