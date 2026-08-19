namespace Words.Core;

/// <summary>
/// A web search engine, and the URL that asks it about an answer.
/// </summary>
/// <remarks>
/// <para>
/// Nothing to do with searching the lexicon: this engine is somebody else's, on the web,
/// reached by handing a URL to the browser. "Search key" and "search engine" are unrelated
/// despite the shared word.
/// </para>
/// <para>
/// It lives in <c>Words.Core</c> rather than in a front end for the reason
/// <see cref="MatchOrdering"/> does — the app needs it now and the planned web app will
/// need exactly the same URLs, and two copies would drift. It is pure string work and
/// brings no dependency with it; opening the URL is the front end's job, because only a
/// front end knows what a browser is.
/// </para>
/// </remarks>
/// <param name="Id">
/// A stable identifier for the chosen engine, safe to persist. The display <paramref name="Name"/>
/// is not: it is the sort of thing that gets retitled or translated.
/// </param>
/// <param name="Name">The engine's name, as a person would recognise it.</param>
/// <param name="QueryPrefix">
/// Everything up to and including the query parameter, so a URL is this followed by the
/// escaped query. Every engine here takes its query last, which is what makes a prefix
/// enough.
/// </param>
public sealed record SearchEngine(string Id, string Name, string QueryPrefix)
{
    public static SearchEngine Google { get; } = new("google", "Google", "https://www.google.com/search?q=");

    public static SearchEngine Bing { get; } = new("bing", "Bing", "https://www.bing.com/search?q=");

    public static SearchEngine DuckDuckGo { get; } = new("duckduckgo", "DuckDuckGo", "https://duckduckgo.com/?q=");

    public static SearchEngine Brave { get; } = new("brave", "Brave", "https://search.brave.com/search?q=");

    public static SearchEngine Ecosia { get; } = new("ecosia", "Ecosia", "https://www.ecosia.org/search?q=");

    public static SearchEngine Startpage { get; } =
        new("startpage", "Startpage", "https://www.startpage.com/sp/search?query=");

    public static SearchEngine Yahoo { get; } = new("yahoo", "Yahoo", "https://search.yahoo.com/search?p=");

    /// <summary>
    /// What a lookup uses until someone chooses otherwise. Google, because it is what most
    /// people already have set, and a default nobody notices is the right one (UI.md).
    /// </summary>
    public static SearchEngine Default => Google;

    /// <summary>
    /// Every engine that can be chosen, in the order they should be offered.
    /// </summary>
    /// <remarks>
    /// Deliberately short. Each entry is one more thing to read past in a list that most
    /// people will open once, if ever.
    /// </remarks>
    public static IReadOnlyList<SearchEngine> All { get; } =
        [Google, Bing, DuckDuckGo, Brave, Ecosia, Startpage, Yahoo];

    /// <summary>
    /// The engine with this identifier, or <see cref="Default"/> when it is unrecognised.
    /// </summary>
    /// <remarks>
    /// Forgiving on purpose: the identifier comes back from a saved preference, which may
    /// have been written by an older version that offered an engine this one does not. A
    /// stale setting should quietly fall back, not fail a lookup.
    /// </remarks>
    public static SearchEngine ById(string? id) =>
        All.FirstOrDefault(engine => string.Equals(engine.Id, id, StringComparison.OrdinalIgnoreCase))
            ?? Default;

    /// <summary>
    /// The URL that asks this engine about an answer.
    /// </summary>
    /// <param name="displayForm">
    /// The answer as a person reads it. The display form rather than the search key, because
    /// the question is going to a reader of English: <c>naïve</c> and <c>inlet's</c> are what
    /// a dictionary is indexed under, <c>NAIVE</c> and <c>INLETS</c> are not.
    /// </param>
    /// <param name="kind">What is being asked.</param>
    public string UrlFor(string displayForm, LookupKind kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayForm);

        return QueryPrefix + Uri.EscapeDataString(QueryFor(displayForm.Trim(), kind));
    }

    /// <summary>
    /// The words typed into the engine's own box.
    /// </summary>
    /// <remarks>
    /// Phrased the way a person would, because that is what the engines are tuned for: all
    /// of them answer "define X" and "X synonyms" with a dictionary or thesaurus panel
    /// rather than a page of results.
    /// </remarks>
    private static string QueryFor(string displayForm, LookupKind kind) => kind switch
    {
        LookupKind.Definition => $"define {displayForm}",
        LookupKind.Synonyms => $"{displayForm} synonyms",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown lookup kind."),
    };

    /// <summary>The name, so a list of engines reads as names without further ceremony.</summary>
    public override string ToString() => Name;
}
