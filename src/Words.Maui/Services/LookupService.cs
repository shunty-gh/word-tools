using Words.Core;

namespace Words.Maui.Services;

/// <summary>
/// Looks an answer up on the web, in whichever search engine the user prefers.
/// </summary>
/// <remarks>
/// <para>
/// The solver knows which words fit; it does not know what they mean. Rather than ship a
/// dictionary and a thesaurus — neither of which the lexicon contains, and both of which
/// carry their own licensing — a lookup hands the question to the browser, where the user
/// already has whatever they trust.
/// </para>
/// <para>
/// <see cref="BrowserLaunchMode.External"/> rather than an in-app view: the user asked to
/// see a definition, not to browse inside a crossword solver, and their own browser has
/// their history, their sign-ins and their ad blocker. It also keeps the app honest —
/// nothing here opens a socket, so the Android manifest still requests no network
/// permission.
/// </para>
/// </remarks>
public sealed class LookupService
{
    /// <summary>
    /// Where the chosen engine is remembered. The engine's stable identifier is stored, not
    /// its name or its position in a list, so neither renaming nor reordering can silently
    /// change what someone chose.
    /// </summary>
    private const string EngineKey = "lookup.engine";

    private SearchEngine _engine =
        SearchEngine.ById(Preferences.Default.Get(EngineKey, SearchEngine.Default.Id));

    /// <summary>
    /// The engine lookups go to. Setting it remembers the choice. The list to choose from is
    /// <see cref="SearchEngine.All"/> — this service owns the choice, not the catalogue.
    /// </summary>
    public SearchEngine Engine
    {
        get => _engine;
        set
        {
            // Normalised on the way in, so what is saved is always something a later launch
            // can resolve — including if a picker ever hands back nothing at all.
            _engine = SearchEngine.ById(value?.Id);
            Preferences.Default.Set(EngineKey, _engine.Id);
        }
    }

    /// <summary>
    /// Opens the browser on this answer. False when no browser could be reached, which the
    /// caller should say something about rather than leaving the button looking broken.
    /// </summary>
    public Task<bool> OpenAsync(string displayForm, LookupKind kind) =>
        Browser.Default.OpenAsync(new Uri(Engine.UrlFor(displayForm, kind)), BrowserLaunchMode.External);
}
