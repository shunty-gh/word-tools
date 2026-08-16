using System.CommandLine;
using Words.Core;

namespace Words.Cli;

/// <summary>How answers are ordered for display.</summary>
internal enum SortOrder
{
    /// <summary>Alphabetical, ignoring case. The default.</summary>
    Alpha,

    /// <summary>Most likely first.</summary>
    Score,

    /// <summary>Shortest first. Only distinguishes composed answers, which vary in words.</summary>
    Length,
}

/// <summary>What a query should return and how it should be shown.</summary>
internal sealed record QuerySettings(EntryFilter Filter, bool Json, int Limit, SortOrder Sort);

/// <summary>
/// The options shared by <c>words pattern</c> and <c>words anagram</c>, so the two cannot
/// drift apart.
/// </summary>
internal sealed class QueryOptions
{
    public Option<bool> Json { get; } = new("--json")
    {
        Description = "Write answers as JSON instead of one per line.",
    };

    /// <summary>
    /// Nullable rather than carrying a sentinel default, so the help does not advertise a
    /// magic number as though it were a real limit. Null means the command chooses.
    /// </summary>
    public Option<int?> Limit { get; } = new("--limit")
    {
        Description = "Show at most this many answers. 0 for no limit.",
        HelpName = "n",
    };

    /// <summary>
    /// No <c>DefaultValueFactory</c>: <see cref="SortOrder.Alpha"/> is the zero value, so
    /// binding produces it anyway, and declaring it would print "[default: Alpha]" against
    /// a list of lowercase names.
    /// </summary>
    public Option<SortOrder> Sort { get; } = new("--sort")
    {
        Description = "Order answers. Alphabetical unless you say otherwise.",
        HelpName = "alpha|score|length",
    };

    public Option<Sources[]> Source { get; } = new("--source")
    {
        Description = "Only use entries from these word lists. All of them by default.",
        HelpName = "esdb|nediger|personal",
        AllowMultipleArgumentsPerToken = true,
    };

    public Option<bool> IncludeRacy { get; } = new("--include-racy")
    {
        Description = "Include entries the word list flagged as potentially racy.",
    };

    public void AddTo(Command command)
    {
        ArgumentNullException.ThrowIfNull(command);

        command.Options.Add(Json);
        command.Options.Add(Limit);
        command.Options.Add(Sort);
        command.Options.Add(Source);
        command.Options.Add(IncludeRacy);
    }

    /// <param name="defaultLimit">
    /// Used when <c>--limit</c> was not given. Composition supplies a cap of its own; the
    /// other queries return small enough result sets not to need one.
    /// </param>
    public QuerySettings Read(ParseResult parseResult, int defaultLimit)
    {
        ArgumentNullException.ThrowIfNull(parseResult);

        var limit = parseResult.GetValue(Limit) switch
        {
            null => defaultLimit,
            0 => int.MaxValue,
            var requested => requested.Value,
        };

        var sources = parseResult.GetValue(Source) ?? [];

        return new QuerySettings(
            new EntryFilter
            {
                Sources = sources.Length == 0 ? Sources.All : sources.Aggregate(Sources.None, (all, s) => all | s),
                IncludeRacy = parseResult.GetValue(IncludeRacy),
            },
            parseResult.GetValue(Json),
            limit,
            parseResult.GetValue(Sort));
    }
}
