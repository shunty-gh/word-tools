namespace Words.Cli;

/// <summary>
/// What the process returns. Follows grep, so the codes mean what a shell script expects
/// them to mean.
/// </summary>
internal static class ExitCodes
{
    /// <summary>Answers were found.</summary>
    public const int Found = 0;

    /// <summary>The query was fine, but nothing matched.</summary>
    public const int NothingFound = 1;

    /// <summary>Something was wrong with the request.</summary>
    public const int BadRequest = 2;

    /// <summary>
    /// Interrupted, by convention 128 + SIGINT. Distinct from <see cref="NothingFound"/>
    /// on purpose: a query abandoned part-way has not established that nothing matches.
    /// </summary>
    public const int Interrupted = 130;
}
