using Words.Core;

namespace Words.Maui.Services;

/// <summary>
/// Which web search engine an answer is looked up with, remembered between launches.
/// </summary>
/// <remarks>
/// Saved as the engine's id rather than its position in <see cref="WordLookup.Engines"/>, so
/// adding an engine cannot silently change what an existing choice means; an id that is no
/// longer offered falls back to the default rather than failing. <c>Preferences</c> is the
/// per-platform settings store — the registry, <c>NSUserDefaults</c>, shared preferences —
/// which is where a person expects a preference to live, and it survives app updates.
/// </remarks>
public sealed class LookupSettings
{
    private const string EngineKey = "lookup.engine";

    public WebSearchEngine Engine
    {
        get => WordLookup.Find(Preferences.Default.Get(EngineKey, WordLookup.Default.Id));
        set => Preferences.Default.Set(EngineKey, value.Id);
    }
}
