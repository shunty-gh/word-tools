using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
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
/// while filling a grid, and the two ways of asking the web about it.
/// </summary>
/// <remarks>
/// The commands are the view model's own, handed to every row rather than made per row: a
/// search can produce hundreds of rows, and they all do the same two things.
/// </remarks>
public sealed record AnswerRow(string Answer, string Tag, ICommand Define, ICommand Synonyms);

public sealed partial class SearchViewModel(
    LexiconService lexicon,
    IPersonalWordStore personalWords,
    LookupService lookups)
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

    // -- where a lookup goes --

    /// <summary>
    /// Named <c>…Names</c> rather than <c>LookupSites</c> because that is the name of the
    /// type these come from, and a property of that name would hide it inside this class.
    /// </summary>
    public IReadOnlyList<string> LookupSiteNames { get; } = [.. LookupSites.All.Select(site => site.Name)];

    /// <summary>Starts on whatever the user last chose, which is why it is not simply 0.</summary>
    [ObservableProperty]
    public partial int LookupSiteIndex { get; set; } = LookupSites.IndexOf(lookups.Site);

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

    /// <summary>
    /// A picker can report -1 while it is being set up, which is not a site.
    /// </summary>
    partial void OnLookupSiteIndexChanged(int value)
    {
        if (value >= 0 && value < LookupSites.All.Count)
        {
            lookups.Site = LookupSites.All[value];
        }
    }

    [RelayCommand]
    private void ToggleOptions() => ShowOptions = !ShowOptions;

    /// <summary>
    /// Looks the answer up in the browser. Separate commands rather than one taking a kind,
    /// because a <see cref="Microsoft.Maui.Controls.Button"/> can carry only one command
    /// parameter and the answer is the more useful thing to put in it.
    /// </summary>
    [RelayCommand]
    private Task LookUpDefinitionAsync(string? answer) => LookUpAsync(LookupKind.Definition, answer);

    [RelayCommand]
    private Task LookUpSynonymsAsync(string? answer) => LookUpAsync(LookupKind.Synonyms, answer);

    private async Task LookUpAsync(LookupKind kind, string? answer)
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            return;
        }

        try
        {
            await lookups.OpenAsync(kind, answer).ConfigureAwait(true);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            // Nothing the user did is wrong here, and there is nothing to retry, so this
            // goes to the status line rather than a dialog they would have to dismiss.
            Status = $"Couldn't open {lookups.Site.Name} in a browser.";
        }
    }

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

            // Rows are built here rather than on the background thread, so the commands they
            // carry are first touched on the thread that will invoke them.
            foreach (var match in found.Shown)
            {
                Answers.Add(new AnswerRow(
                    match.DisplayForm,
                    TagFor(match),
                    LookUpDefinitionCommand,
                    LookUpSynonymsCommand));
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

    private sealed record Found(IReadOnlyList<Match> Shown, int Total);

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
