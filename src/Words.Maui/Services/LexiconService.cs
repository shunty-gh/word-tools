using Words.Core;

namespace Words.Maui.Services;

/// <summary>
/// Loads the lexicon once and hands out an engine over it.
/// </summary>
/// <remarks>
/// Loading takes over a hundred milliseconds and building the anagram index takes longer
/// again, so it must never happen on the UI thread. The task is started once and awaited by
/// whoever needs it, so a second caller arriving mid-load simply waits rather than starting
/// a second load.
/// </remarks>
public sealed class LexiconService(IPersonalWordStore personalWords)
{
    private readonly Lock _gate = new();
    private Task<WordEngine>? _engine;

    public Task<WordEngine> GetEngineAsync()
    {
        lock (_gate)
        {
            return _engine ??= Task.Run(LoadAsync);
        }
    }

    /// <summary>
    /// Starts loading without waiting, so the work overlaps the first frame being drawn.
    /// </summary>
    public void BeginLoading() => _ = GetEngineAsync();

    private async Task<WordEngine> LoadAsync()
    {
        var lexicon = await Lexicon.LoadAsync(
        [
            EmbeddedLexicon.Source,
            new PersonalLexiconSource(personalWords),
        ]).ConfigureAwait(false);

        return new WordEngine(lexicon);
    }
}
