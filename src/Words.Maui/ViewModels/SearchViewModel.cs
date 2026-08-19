using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Words.Core;
using Words.Maui.Services;

namespace Words.Maui.ViewModels;

/// <summary>
/// One square in the strip beneath the input: a letter you have, or a gap you do not.
/// </summary>
/// <param name="Letter">The letter, or empty for a gap.</param>
/// <param name="IsChoice">A <c>[abc]</c> class — one cell, several possible letters.</param>
public sealed record LetterCell(string Letter, bool IsChoice)
{
    public static LetterCell Gap { get; } = new(string.Empty, IsChoice: false);

    public bool IsKnown => Letter.Length > 0;
}

/// <summary>
/// One answer, with a short tag when there is something about it worth knowing at a glance
/// while filling a grid, and the links that look it up on the web.
/// </summary>
/// <param name="CanLookUp">
/// Whether looking this answer up would mean anything. A composition is several words that
/// happen to use the right letters, so it has no definition and no synonyms; the links are
/// hidden rather than shown leading nowhere useful.
/// </param>
public sealed record AnswerRow(string Answer, string Tag, bool CanLookUp, AnswerLookup Lookup);

public sealed partial class SearchViewModel : ObservableObject
{
    /// <summary>
    /// A list view will happily bind thousands of rows, but nobody reads them, and a broad
    /// composition can produce tens of thousands. The count reported below is the true one.
    /// </summary>
    private const int DisplayLimit = 500;

    /// <summary>Dims the label on the side of the switch that is not active.</summary>
    private const double Inactive = 0.4;

    private readonly LexiconService _lexicon;
    private readonly IPersonalWordStore _personalWords;
    private readonly LookupSettings _lookupSettings;

    /// <summary>The links carried by every answer row — see <see cref="AnswerLookup"/>.</summary>
    private readonly AnswerLookup _lookup;

    private CancellationTokenSource? _running;

    public SearchViewModel(
        LexiconService lexicon,
        IPersonalWordStore personalWords,
        LookupSettings lookupSettings)
    {
        _lexicon = lexicon;
        _personalWords = personalWords;
        _lookupSettings = lookupSettings;
        _lookup = new AnswerLookup(lookupSettings, message => Status = message);

        SearchEngineIndex = WebSearchEngines.IndexOf(lookupSettings.Engine.Name);
    }

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

    // -- which search engine the links on an answer use --

    public IReadOnlyList<string> SearchEngines { get; } = [.. WebSearchEngines.All.Select(e => e.Name)];

    [ObservableProperty]
    public partial int SearchEngineIndex { get; set; }

    /// <summary>
    /// Remembered, so the choice is made once rather than every session. A picker reports
    /// -1 when it has no selection, which is not a choice and must not be stored.
    /// </summary>
    partial void OnSearchEngineIndexChanged(int value)
    {
        if (value >= 0 && value < WebSearchEngines.All.Count)
        {
            _lookupSettings.Engine = WebSearchEngines.All[value];
        }
    }

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

    public ObservableCollection<AnswerRow> Answers { get; } = [];

    /// <summary>
    /// The query drawn as crossword cells. Presentational only and deliberately forgiving —
    /// it never throws, and a malformed pattern is reported by the status line, not here.
    /// </summary>
    public ObservableCollection<LetterCell> Cells { get; } = [];

    public bool HasCells => Cells.Count > 0;

    // Short enough to survive a phone's width. The longer wording these replaced was cut off
    // mid-sentence on Android with no ellipsis, so the placeholder stopped teaching the
    // syntax, which is the only reason it is there. `blank` is the term CONTEXT.md settles on
    // for an anagram's unknown letter.
    public string Placeholder => IsAnagram
        ? "Your letters, with . for a blank"
        : "Letters and gaps, like A..D";

    public double CrosswordOpacity => IsAnagram ? Inactive : 1;

    public double AnagramOpacity => IsAnagram ? 1 : Inactive;

    public string OptionsLabel => ShowOptions ? "Options ▲" : "Options ▼";

    partial void OnIsAnagramChanged(bool value)
    {
        OnPropertyChanged(nameof(Placeholder));
        OnPropertyChanged(nameof(CrosswordOpacity));
        OnPropertyChanged(nameof(AnagramOpacity));
        OnPropertyChanged(nameof(EmptyMessage));
    }

    partial void OnQueryChanged(string value)
    {
        Cells.Clear();

        foreach (var cell in ReadCells(value))
        {
            Cells.Add(cell);
        }

        OnPropertyChanged(nameof(HasCells));
    }

    /// <summary>
    /// Reads the query as squares: a letter fills one, '.' or '?' leaves one empty, an
    /// ellipsis stands for the three dots it replaced, and '[abc]' is a single cell with
    /// several possible letters. Spaces and punctuation are ignored, as the parsers do.
    /// </summary>
    private static IEnumerable<LetterCell> ReadCells(string input)
    {
        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];

            switch (c)
            {
                case '?' or '.':
                    yield return LetterCell.Gap;
                    break;

                case '\u2026':
                    yield return LetterCell.Gap;
                    yield return LetterCell.Gap;
                    yield return LetterCell.Gap;
                    break;

                case '[':
                    while (i < input.Length && input[i] != ']')
                    {
                        i++;
                    }

                    yield return new LetterCell("\u00b7", IsChoice: true);
                    break;

                default:
                    if (char.IsLetter(c))
                    {
                        yield return new LetterCell(char.ToUpperInvariant(c).ToString(), IsChoice: false);
                    }

                    break;
            }
        }
    }

    public string EmptyMessage => "Answers appear here.";

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

        await _personalWords.AddAsync(word).ConfigureAwait(true);
        _lexicon.Invalidate();

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
            var engine = await _lexicon.GetEngineAsync().ConfigureAwait(true);
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

    private sealed record Found(IReadOnlyList<AnswerRow> Shown, int Total);

    /// <summary>
    /// A short tag, when there is something worth knowing at a glance while filling a grid.
    /// Your own words come first: that an answer is one you added matters more than what
    /// kind of word it is.
    /// </summary>
    private static string TagFor(Match match)
    {
        if (match.Components.Any(c => c.Sources.HasFlag(Core.Sources.Personal)))
        {
            return "yours";
        }

        if (match.Components.Any(c => c.Kinds.HasFlag(EntryKinds.ProperNoun)))
        {
            return "name";
        }

        return match.Components.Any(c => c.Kinds.HasFlag(EntryKinds.Phrase)) ? "phrase" : string.Empty;
    }

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

        return new Found(
            [.. shown.Select(m => new AnswerRow(m.DisplayForm, TagFor(m), !m.IsComposition, _lookup))],
            all.Count);
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
