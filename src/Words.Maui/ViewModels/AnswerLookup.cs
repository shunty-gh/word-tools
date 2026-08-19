using CommunityToolkit.Mvvm.Input;
using Words.Core;
using Words.Maui.Services;

namespace Words.Maui.ViewModels;

/// <summary>
/// The links on an answer row: what the word means, and what else means the same. Both hand
/// a search address to the browser and leave the app.
/// </summary>
/// <remarks>
/// One instance is shared by every row rather than one per answer. The commands take the
/// answer as their parameter, so a list of five hundred needs one of these, not five hundred.
/// </remarks>
/// <param name="settings">The chosen engine, read at the moment of the tap so a change takes
/// effect without researching.</param>
/// <param name="report">Where to say so when no browser could be opened. The app has one
/// status line and it belongs to the search view model.</param>
public sealed partial class AnswerLookup(LookupSettings settings, Action<string> report)
{
    [RelayCommand]
    private Task DefineAsync(string? answer) => OpenAsync(LookupKind.Definition, answer);

    [RelayCommand]
    private Task SynonymsAsync(string? answer) => OpenAsync(LookupKind.Synonyms, answer);

    private async Task OpenAsync(LookupKind kind, string? answer)
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            return;
        }

        try
        {
            // Launcher rather than Browser: the ask is to leave for the browser the user
            // already has, not to open a web view inside a crossword solver. It also means
            // the app itself still never opens a connection.
            if (!await Launcher.Default.OpenAsync(settings.Engine.UrlFor(kind, answer)).ConfigureAwait(true))
            {
                report($"Nothing on this device offered to open a web page for '{answer}'.");
            }
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            report($"Could not open a browser: {error.Message}");
        }
    }
}
