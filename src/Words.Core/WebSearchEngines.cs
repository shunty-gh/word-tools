using System.Globalization;

namespace Words.Core;

/// <summary>
/// What a solver wants to know about an answer once they have it.
/// </summary>
public enum LookupKind
{
    /// <summary>What the answer means.</summary>
    Definition,

    /// <summary>Other words that mean the same.</summary>
    Synonyms,
}

/// <summary>
/// A web search engine an answer can be looked up with.
/// </summary>
/// <remarks>
/// Presentation rather than engine, and it lives here for the same reason
/// <see cref="MatchOrdering"/> does: every front end that shows answers wants the same
/// links, and the alternative is each of them keeping its own copy of these URLs. Nothing
/// here opens a connection — it only builds an address for a browser to be handed.
/// </remarks>
/// <param name="Name">
/// What the engine is called, as shown to a person. Also how a chosen engine is stored, so
/// that reordering <see cref="WebSearchEngines.All"/> cannot silently change a saved choice.
/// </param>
/// <param name="UrlTemplate">
/// The engine's search address with <c>{0}</c> where the encoded terms belong. Engines do
/// not agree on the parameter's name — <c>q</c>, <c>query</c> and <c>p</c> are all in use —
/// so the whole address is held rather than a host.
/// </param>
public sealed record WebSearchEngine(string Name, string UrlTemplate)
{
    /// <summary>Where to send a browser to look this answer up.</summary>
    /// <param name="kind">What the solver wants to know.</param>
    /// <param name="displayForm">
    /// The answer as a person reads it. The display form, not the search key: someone
    /// looking up <c>naïve</c> or <c>red herring</c> means those words, not <c>NAIVE</c>
    /// or <c>REDHERRING</c>, and a search engine wants what they would have typed.
    /// </param>
    public Uri UrlFor(LookupKind kind, string displayForm)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayForm);

        var terms = TermsFor(kind, displayForm.Trim());

        return new Uri(string.Format(
            CultureInfo.InvariantCulture,
            UrlTemplate,
            Uri.EscapeDataString(terms)));
    }

    /// <summary>
    /// What to search for. Phrased the way a person would type it, because that is what
    /// every one of these engines is tuned to answer with a dictionary or thesaurus card.
    /// </summary>
    private static string TermsFor(LookupKind kind, string displayForm) => kind switch
    {
        LookupKind.Definition => $"define {displayForm}",
        LookupKind.Synonyms => $"{displayForm} synonyms",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a lookup kind."),
    };
}

/// <summary>
/// The engines an answer can be looked up with, and the one used unless a person says
/// otherwise.
/// </summary>
/// <remarks>
/// Google leads because it is the default; the rest are alphabetical, so a list that grows
/// stays predictable to read. This is a fixed list rather than the platform's own default
/// browser search setting, which no platform exposes to an app.
/// </remarks>
public static class WebSearchEngines
{
    /// <summary>Every engine on offer, in the order a chooser should show them.</summary>
    public static IReadOnlyList<WebSearchEngine> All { get; } =
    [
        new("Google", "https://www.google.com/search?q={0}"),
        new("Bing", "https://www.bing.com/search?q={0}"),
        new("Brave", "https://search.brave.com/search?q={0}"),
        new("DuckDuckGo", "https://duckduckgo.com/?q={0}"),
        new("Ecosia", "https://www.ecosia.org/search?q={0}"),
        new("Startpage", "https://www.startpage.com/sp/search?query={0}"),
        new("Yahoo", "https://search.yahoo.com/search?p={0}"),
    ];

    /// <summary>The engine used until someone chooses another.</summary>
    public static WebSearchEngine Default => All[0];

    /// <summary>
    /// The engine stored under this name, or <see cref="Default"/> when the name is absent
    /// or no longer one we offer — a saved choice must never leave the app without links.
    /// </summary>
    public static WebSearchEngine ByName(string? name) => All[IndexOf(name)];

    /// <summary>
    /// Where a stored name sits in <see cref="All"/>, or the default's position when it is
    /// absent or unknown. Front ends show the list and need the selection back as an index.
    /// </summary>
    public static int IndexOf(string? name)
    {
        for (var i = 0; i < All.Count; i++)
        {
            if (string.Equals(All[i].Name, name, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return 0;
    }
}
