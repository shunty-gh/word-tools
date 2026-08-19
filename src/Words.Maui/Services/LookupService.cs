using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using Words.Core;

namespace Words.Maui.Services;

/// <summary>
/// Opens an answer in a web search, in whichever engine the user picked.
/// </summary>
/// <remarks>
/// <para>
/// The app can say an answer fits; it cannot say what the answer means, because the lexicon
/// holds no definitions. Handing that question to the browser is the honest way to answer it,
/// and it is what a solver does anyway once a word they do not recognise appears.
/// </para>
/// <para>
/// The browser is the system's own, not an in-app tab: what was asked for is a link out, and
/// a half-app half-browser view leaves the user unsure which one the back gesture will
/// dismiss. Note that no network permission is needed for this — the app hands a URL to the
/// system and the browser does the fetching, so the release manifest still requests nothing.
/// </para>
/// </remarks>
public sealed class LookupService
{
    /// <summary>
    /// The site is stored by name rather than by index, so reordering
    /// <see cref="LookupSites.All"/> cannot silently switch someone's engine.
    /// </summary>
    private const string SiteSetting = "lookup-site";

    private LookupSite _site = LookupSites.ByName(Preferences.Default.Get(SiteSetting, LookupSites.Default.Name));

    /// <summary>
    /// The engine lookups are sent to. Remembered across launches: it is a choice about the
    /// user's own tools, and asking again every session would be asking twice for nothing.
    /// </summary>
    public LookupSite Site
    {
        get => _site;
        set
        {
            _site = value;
            Preferences.Default.Set(SiteSetting, value.Name);
        }
    }

    /// <summary>Opens the search in the default browser.</summary>
    public Task<bool> OpenAsync(LookupKind kind, string displayForm) =>
        Browser.Default.OpenAsync(Site.UriFor(kind, displayForm), BrowserLaunchMode.External);
}
