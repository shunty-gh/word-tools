using System.Reflection;

namespace Words.Core;

/// <summary>The terms something shipped with the app is used under.</summary>
public sealed record Licence(string Source, string Summary, string Text);

/// <summary>
/// The terms this software and its bundled word lists are used under.
/// </summary>
/// <remarks>
/// Every one of these has to be reproducible by a running program, not merely present in
/// the repository: the Apache licence requires a copy to reach anyone the program is
/// distributed to, and both word lists require their notice to accompany anything
/// distributed. A self-contained binary has nothing beside it, so the texts are embedded
/// in the assembly. Any front end needs a way to display them — <c>words licence</c> and
/// the About screen exist for that. See
/// <see href="../../docs/adr/0004-scowl-nediger-lexicon.md">ADR 0004</see>.
/// </remarks>
public static class Licences
{
    /// <summary>
    /// This software's own terms, which cover the program but not the word lists it
    /// bundles — those are third-party and stay under the terms in <see cref="WordLists"/>.
    /// </summary>
    public static Licence Program { get; } =
        new(
            "Words",
            "Apache Licence 2.0, Copyright © 2026 Steven Hunt. Free to use, modify and "
                + "redistribute, provided this notice travels with it. Covers the program "
                + "itself; the word lists below are separate and keep their own terms.",
            Read("Words.Core.licences.apache-2.0.txt"));

    /// <summary>The terms of the word lists the lexicon is built from.</summary>
    public static IReadOnlyList<Licence> WordLists { get; } =
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
        using var stream = typeof(Licences).GetTypeInfo().Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"The licence text '{resourceName}' is missing from the assembly.");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().TrimEnd();
    }
}
