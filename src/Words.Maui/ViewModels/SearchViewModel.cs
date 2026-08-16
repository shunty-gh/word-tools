using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Words.Core;
using Words.Maui.Services;

namespace Words.Maui.ViewModels;

public sealed partial class SearchViewModel(LexiconService lexicon) : ObservableObject
{
    /// <summary>
    /// A list view will happily bind thousands of rows, but nobody reads them, and a broad
    /// composition can produce tens of thousands. The count reported below is the true one.
    /// </summary>
    private const int DisplayLimit = 500;

    private CancellationTokenSource? _running;

    [ObservableProperty]
    public partial string Query { get; set; } = string.Empty;

    /// <summary>
    /// False solves a crossword pattern, true solves an anagram. A plain switch rather than
    /// a list: there are only two, and a dropdown for two options is a click for nothing.
    /// </summary>
    [ObservableProperty]
    public partial bool IsAnagram { get; set; }

    [ObservableProperty]
    public partial bool Compose { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string Status { get; set; } = "Ready.";

    public ObservableCollection<string> Answers { get; } = [];

    public string Placeholder => IsAnagram
        ? "Your letters, with . for each one you don't know"
        : "Letters and gaps, like A..D or RED.ERRING";

    /// <summary>Dims the label on the side of the switch that is not active.</summary>
    private const double Inactive = 0.4;

    public double CrosswordOpacity => IsAnagram ? Inactive : 1;

    public double AnagramOpacity => IsAnagram ? 1 : Inactive;

    partial void OnIsAnagramChanged(bool value)
    {
        OnPropertyChanged(nameof(Placeholder));
        OnPropertyChanged(nameof(CrosswordOpacity));
        OnPropertyChanged(nameof(AnagramOpacity));
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(Query))
        {
            Answers.Clear();
            Status = "Type something to search for.";
            return;
        }

        // Abandon an in-flight search: the user has moved on, and a broad composition can
        // run for seconds.
        await CancelRunningAsync().ConfigureAwait(true);

        using var running = new CancellationTokenSource();
        _running = running;

        IsBusy = true;
        Status = "Searching…";
        Answers.Clear();

        try
        {
            var stopwatch = Stopwatch.StartNew();
            var engine = await lexicon.GetEngineAsync().ConfigureAwait(true);
            var found = await Task.Run(
                () => CollectAsync(engine, running.Token),
                running.Token).ConfigureAwait(true);

            foreach (var answer in found.Shown)
            {
                Answers.Add(answer);
            }

            Status = Describe(found, stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            Status = "Cancelled.";
        }
        catch (QuerySyntaxException error)
        {
            Status = error.Message;
        }
        catch (ArgumentOutOfRangeException error)
        {
            Status = error.Message.Split(" (Parameter")[0];
        }
        finally
        {
            IsBusy = false;
            _running = null;
        }
    }

    private async Task CancelRunningAsync()
    {
        if (_running is { } running)
        {
            await running.CancelAsync().ConfigureAwait(true);
        }
    }

    private sealed record Found(IReadOnlyList<string> Shown, int Total);

    private async Task<Found> CollectAsync(WordEngine engine, CancellationToken cancellationToken)
    {
        var matches = IsAnagram
            ? engine.QueryAsync(
                new AnagramQuery
                {
                    Letters = Query,
                    Compose = Compose ? CompositionOptions.Default : null,
                },
                cancellationToken)
            : engine.QueryAsync(new PatternQuery { Pattern = Query }, cancellationToken);

        var all = new List<Match>();

        await foreach (var match in matches.ConfigureAwait(false))
        {
            all.Add(match);
        }

        // Same rule as the CLI: when truncating, keep the most likely and only then sort
        // for display, so the cap does not simply keep everything beginning with A.
        var shown = all
            .OrderBy(m => m.Components.Count)
            .ThenByDescending(m => m.Score)
            .Take(DisplayLimit)
            .OrderBy(m => m.DisplayForm, StringComparer.OrdinalIgnoreCase)
            .Select(m => m.DisplayForm)
            .ToList();

        return new Found(shown, all.Count);
    }

    private static string Describe(Found found, long elapsedMs)
    {
        if (found.Total == 0)
        {
            return "No answers.";
        }

        var answers = found.Total == 1 ? "1 answer" : $"{found.Total:N0} answers";
        var shown = found.Shown.Count < found.Total
            ? $", showing the {found.Shown.Count:N0} most likely"
            : string.Empty;

        return $"{answers}{shown} in {elapsedMs:N0} ms.";
    }
}
