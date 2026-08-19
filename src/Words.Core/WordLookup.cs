namespace Words.Core;

/// <summary>What a solver wants to know about an answer once the grid has produced it.</summary>
public enum LookupKind
{
    /// <summary>What it means.</summary>
    Definition,

    /// <summary>Other words that mean the same.</summary>
    Synonyms,
}

/// <summary>
/// A web search engine, and how to ask it about an answer.
/// </summary>
/// <remarks>
/// "Engine" here is the user's word for Google or Bing, not <see cref="WordEngine"/>, which
/// is the solver. Nothing here connects to anything: it builds a URL and stops, leaving a
/// front end to hand that URL to the platform's browser.
/// </remarks>
/// <param name="Id">
/// A stable name for the engine, for a front end that remembers the user's choice. Saving
/// the position in <see cref="WordLookup.Engines"/> instead would silently change what a
/// saved choice meant the day the list grew.
/// </param>
/// <param name="Name">The engine as a person knows it, for a menu.</param>
/// <param name="QueryUrl">
/// Everything up to and including the query parameter, so the encoded terms are simply
/// appended. A prefix rather than a template because engines disagree on the parameter's
/// name — Yahoo's is <c>p</c>, everyone else's is <c>q</c> — but not on where it goes.
/// </param>
public sealed record WebSearchEngine(string Id, string Name, string QueryUrl)
{
    /// <summary>
    /// The URL that asks this engine about <paramref name="displayForm"/>.
    /// </summary>
    /// <remarks>
    /// The display form, not the search key: a dictionary knows <c>naïve</c> and
    /// <c>Red Herring</c>, and knows nothing of <c>NAIVE</c> or <c>REDHERRING</c>.
    /// </remarks>
    /// <exception cref="ArgumentException">The answer has nothing to look up.</exception>
    public string UrlFor(LookupKind kind, string displayForm)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayForm);

        var answer = displayForm.Trim();

        // Phrased as a person would type it. "define x" is understood as a dictionary
        // request by every engine here, and degrades to an ordinary search where it is not.
        var terms = kind switch
        {
            LookupKind.Definition => $"define {answer}",
            LookupKind.Synonyms => $"{answer} synonyms",
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

        return QueryUrl + Uri.EscapeDataString(terms);
    }
}

/// <summary>
/// Where an answer can be looked up, and what the link should say.
/// </summary>
/// <remarks>
/// Presentation rather than engine, and here for the same reason as
/// <see cref="MatchOrdering"/>: the app and the web front end both need it, and two copies
/// of a list of URLs is how one of them quietly ends up a year out of date.
/// </remarks>
public static class WordLookup
{
    /// <summary>
    /// The engines offered, the default first and the rest in the order a menu shows them.
    /// </summary>
    /// <remarks>
    /// Deliberately short. Every extra entry is a decision asked of someone who came here to
    /// solve a crossword (UI.md), and an engine nobody picks still has to be kept working.
    /// </remarks>
    public static IReadOnlyList<WebSearchEngine> Engines { get; } =
    [
        new("google", "Google", "https://www.google.com/search?q="),
        new("bing", "Bing", "https://www.bing.com/search?q="),
        new("duckduckgo", "DuckDuckGo", "https://duckduckgo.com/?q="),
        new("brave", "Brave", "https://search.brave.com/search?q="),
        new("ecosia", "Ecosia", "https://www.ecosia.org/search?q="),
        new("yahoo", "Yahoo", "https://search.yahoo.com/search?p="),
    ];

    /// <summary>The engine used until someone chooses otherwise.</summary>
    public static WebSearchEngine Default => Engines[0];

    /// <summary>
    /// The engine with this id, or the default if it is unknown.
    /// </summary>
    /// <remarks>
    /// Forgiving on purpose: a saved choice outlives the release that offered it, and an
    /// engine that has since been dropped should cost the user a different search result,
    /// not a broken app.
    /// </remarks>
    public static WebSearchEngine Find(string? id) =>
        Engines.FirstOrDefault(e => string.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase))
        ?? Default;
}
