using Words.Core;

namespace Words.Maui.Services;

/// <summary>
/// Which search engine the links on an answer use.
/// </summary>
/// <remarks>
/// Kept in <see cref="Preferences"/> rather than beside the personal word list: it is a
/// setting about this device, not part of the user's work. The engine's <em>name</em> is
/// stored, never its position in the list, so adding or reordering engines cannot silently
/// change somebody's choice — and a name we no longer offer falls back to the default rather
/// than leaving the answers without links.
/// </remarks>
public sealed class LookupSettings
{
    private const string EngineKey = "lookup.engine";

    public WebSearchEngine Engine
    {
        get => WebSearchEngines.ByName(Preferences.Default.Get(EngineKey, string.Empty));

        set
        {
            ArgumentNullException.ThrowIfNull(value);
            Preferences.Default.Set(EngineKey, value.Name);
        }
    }
}
