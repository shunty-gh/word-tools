using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Words.Core;
using Words.Maui.Services;

namespace Words.Maui.ViewModels;

public sealed partial class SearchViewModel(LexiconService lexicon, IPersonalWordStore personalWords)
    : ObservableObject
{
    /// <summary>
    /// A list view will happily bind thousands of rows, but nobody reads them, and a broad
    /// composition can produce tens of thousands. The count reported below is the true one.
    /// </summary>
    private const int DisplayLimit = 500;

    /// <summary>Dims the label on the side of the switch that is not active.</summary>
    private const double Inactive = 0.4;

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
    public partial bool ShowOptions { get; set; }

    [ObservableProperty]
    public partial string Status { get; set; } = "Ready.";

    // -- options, mirroring the CLI's --sort, --include-racy, --source and compose bounds --

    public IReadOnlyList<string> SortOrders { get; } = ["Alphabetical", "Most likely", "Shortest"];

    [ObservableProperty]
    public partial int SortIndex { get; set; }

    [ObservableProperty]
    public partial bool IncludeRacy { get; set; }

    [ObservableProperty]
    public partial bool OnlyMyWords { get; set; }

    public IReadOnlyList<string> WordCounts { get; } = ["2 words", "3 words"];

    [ObservableProperty]
    public partial int WordCountIndex { get; set; }

    public IReadOnlyList<string> MinLengths { get; } = ["2 letters", "3 letters", "4 letters"];

    [ObservableProperty]
    public partial int MinLengthIndex { get; set; } = 1;

    public ObservableCollection<string> Answers { get; } = [];

    public string Placeholder => IsAnagram
        ? "Your letters, with . for each one you don't know"
        : "Letters and gaps, like A..D or RED.ERRING";

    public double CrosswordOpacity => IsAnagram ? Inactive : 1;

    public double AnagramOpacity => IsAnagram ? 1 : Inactive;

    public string OptionsLabel => ShowOptions ? "Options ▲" : "Options ▼";

    partial void OnIsAnagramChanged(bool value)
    {
        OnPropertyChanged(nameof(Placeholder));
        OnPropertyChanged(nameof(CrosswordOpacity));
        OnPropertyChanged(nameof(AnagramOpacity));
    }

    partial void OnShowOptionsChanged(bool value) => OnPropertyChanged(nameof(OptionsLabel));

    [RelayCommand]
    private void ToggleOptions() => ShowOptions = !ShowOptions;

    /// <summary>
    /// Adds a word to the personal list and discards the loaded lexicon, so the next search
    /// finds it.
    /// </summary>
    public async Task AddWordAsync(string? displayForm)
    {
        var word = displayForm?.Trim() ?? string.Empty;

        if (word.Length == 0)
        {
            return;
        }

        if (SearchKeys.From(word).Length == 0)
        {
            Status = $"'{word}' has no letters, so it could never be an answer.";
            return;
        }

        await personalWords.AddAsync(word).ConfigureAwait(true);
        lexicon.Invalidate();

        Status = $"Added '{word}'. It will be included from your next search.";
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

    private EntryFilter BuildFilter() => new()
    {
        Sources = OnlyMyWords ? Core.Sources.Personal : Core.Sources.All,
        IncludeRacy = IncludeRacy,
    };

    private async Task<Found> CollectAsync(WordEngine engine, CancellationToken cancellationToken)
    {
        var filter = BuildFilter();

        var matches = IsAnagram
            ? engine.QueryAsync(
                new AnagramQuery
                {
                    Letters = Query,
                    Filter = filter,
                    Compose = Compose
                        ? new CompositionOptions
                        {
                            MaxComponents = WordCountIndex + 2,
                            MinComponentLength = MinLengthIndex + 2,
                        }
                        : null,
                },
                cancellationToken)
            : engine.QueryAsync(
                new PatternQuery { Pattern = Query, Filter = filter },
                cancellationToken);

        var all = new List<Match>();

        await foreach (var match in matches.ConfigureAwait(false))
        {
            all.Add(match);
        }

        // Mapped explicitly rather than cast from the index, so reordering the picker's
        // labels cannot silently change what the options mean.
        var sort = SortIndex switch
        {
            1 => SortOrder.Score,
            2 => SortOrder.Length,
            _ => SortOrder.Alpha,
        };

        var shown = MatchOrdering.Arrange(all, sort, DisplayLimit);

        return new Found([.. shown.Select(m => m.DisplayForm)], all.Count);
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
