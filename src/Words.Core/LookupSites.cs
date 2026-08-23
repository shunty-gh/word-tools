namespace Words.Core;

/// <summary>What a lookup asks the web about an answer.</summary>
public enum LookupKind
{
    /// <summary>What the answer means.</summary>
    Definition,

    /// <summary>Other words that could stand in for it.</summary>
    Synonyms,
}

/// <summary>
/// A search engine an answer can be looked up in.
/// </summary>
/// <param name="Name">
/// The engine's name, as shown in the chooser and as stored when the choice is remembered.
/// </param>
/// <param name="SearchUrl">
/// The engine's search URL up to and including its query parameter, so the terms can be
/// appended. The parameter is not always <c>q</c>, which is why the whole prefix is held
/// rather than a host name.
/// </param>
/// <remarks>
/// The lexicon holds no definitions and never will — it is a list of answers, not a
/// dictionary (see <see href="../../CONTEXT.md">CONTEXT.md</see>) — so "what does this mean"
/// is a question for the web. Sending it to a search engine rather than to one dictionary
/// site keeps the app out of the business of choosing a reference work, and lets the user
/// keep whatever engine they already trust.
/// </remarks>
public sealed record LookupSite(string Name, string SearchUrl)
{
    /// <summary>
    /// Where to send a person who wants to know what an answer means, or what else could be
    /// written in its place.
    /// </summary>
    /// <param name="kind">Which question is being asked.</param>
    /// <param name="displayForm">The answer as it is shown — spaces, accents and all.</param>
    public Uri UriFor(LookupKind kind, string displayForm)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayForm);

        return new Uri(SearchUrl + Uri.EscapeDataString(TermsFor(kind, displayForm)));
    }

    /// <summary>
    /// What is actually typed into the engine. Plain words rather than an engine-specific
    /// trick, because every engine here answers <c>define …</c> with a dictionary entry and
    /// none of them need a syntax of their own.
    /// </summary>
    private static string TermsFor(LookupKind kind, string displayForm) => kind switch
    {
        LookupKind.Definition => $"define {displayForm}",
        LookupKind.Synonyms => $"{displayForm} synonyms",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown lookup kind."),
    };
}

/// <summary>
/// The search engines a front end can offer, in the order they are offered.
/// </summary>
/// <remarks>
/// Kept here rather than in a front end because the app and the web front end both need it,
/// and because a URL that quietly stops working should be fixed in one place. Nothing in
/// this type reaches the network: it builds a URL and stops, leaving opening it to whoever
/// has a browser to open it with.
/// </remarks>
public static class LookupSites
{
    /// <summary>
    /// Google first because it is what most people would have chosen anyway, then the
    /// engines someone might have switched to. Long enough that most people find theirs,
    /// short enough that the list is not itself a decision to make (UI.md).
    /// </summary>
    public static IReadOnlyList<LookupSite> All { get; } =
    [
        new("Google", "https://www.google.com/search?q="),
        new("Bing", "https://www.bing.com/search?q="),
        new("DuckDuckGo", "https://duckduckgo.com/?q="),
        new("Yahoo", "https://search.yahoo.com/search?p="),
        new("Ecosia", "https://www.ecosia.org/search?q="),
        new("Brave", "https://search.brave.com/search?q="),
        new("Startpage", "https://www.startpage.com/sp/search?query="),
    ];

    /// <summary>What a front end uses until the user says otherwise.</summary>
    public static LookupSite Default => All[0];

    /// <summary>
    /// The site of that name, or the default if it is not one we know. Forgiving on purpose:
    /// the name is what gets stored in a settings file, so an older or hand-edited setting
    /// must not stop the app from looking anything up.
    /// </summary>
    public static LookupSite ByName(string? name) =>
        All.FirstOrDefault(site => string.Equals(site.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? Default;

    /// <summary>Where a site sits in <see cref="All"/>, for binding to a list control.</summary>
    public static int IndexOf(LookupSite site)
    {
        for (var i = 0; i < All.Count; i++)
        {
            if (All[i] == site)
            {
                return i;
            }
        }

        // Not one of ours, so point at the default, which is the first.
        return 0;
    }
}
