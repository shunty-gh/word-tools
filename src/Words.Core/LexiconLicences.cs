using System.Reflection;

namespace Words.Core;

/// <summary>The terms of a word list bundled with the app.</summary>
public sealed record LexiconLicence(string Source, string Summary, string Text);

/// <summary>
/// The licences of the bundled word lists.
/// </summary>
/// <remarks>
/// Both sources require their notice to accompany anything distributed, so the texts are
/// embedded in the assembly rather than shipped alongside it — a self-contained binary must
/// be able to show them. Any front end needs a way to display these; see
/// <see href="../../docs/adr/0004-scowl-nediger-lexicon.md">ADR 0004</see>.
/// </remarks>
public static class LexiconLicences
{
    public static IReadOnlyList<LexiconLicence> All { get; } =
    [
        new(
            "English Speller Database (ESDB), formerly SCOWL",
            "Copyright © 2000–2026 Kevin Atkinson. Permits distributing word lists created from it, "
                + "provided the notice below is included.",
            Read("Words.Core.licences.esdb.txt")),
        new(
            "Nediger list",
            "MIT, Copyright © 2026 bewilderingly.",
            Read("Words.Core.licences.nediger.txt")),
    ];

    private static string Read(string resourceName)
    {
        using var stream = typeof(LexiconLicences).GetTypeInfo().Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"The licence text '{resourceName}' is missing from the assembly.");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().TrimEnd();
    }
}
