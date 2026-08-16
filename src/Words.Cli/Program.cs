using System.CommandLine;
using System.CommandLine.Help;
using Words.Cli;

var root = new RootCommand("Crossword and anagram solver.");
root.Subcommands.Add(PatternCommand.Create());
root.Subcommands.Add(AnagramCommand.Create());
root.Subcommands.Add(AddCommand.Create());
root.Subcommands.Add(LexiconCommand.Create());
root.Subcommands.Add(LicenceCommand.Create());

var parseResult = root.Parse(args);

// Setting this is what connects Ctrl-C to the CancellationToken the commands are given.
// Without it a broad composition ignores the interrupt and runs to completion, which on a
// long rack is several seconds after the user has given up.
var configuration = new InvocationConfiguration
{
    ProcessTerminationTimeout = TimeSpan.FromSeconds(2),
};

// Help and parse-error output are the only things carrying a usage line, and both are
// small and produced once — so they can be buffered and tidied. Query results are not
// buffered; they stream straight to the console.
var carriesUsageLine = parseResult.Action is HelpAction || parseResult.Errors.Count > 0;

int exitCode;

if (carriesUsageLine)
{
    var output = new StringWriter();
    var error = new StringWriter();

    configuration.Output = output;
    configuration.Error = error;

    exitCode = await parseResult.InvokeAsync(configuration).ConfigureAwait(false);

    Console.Error.Write(HelpText.WithQuotesOutsidePlaceholders(error.ToString()));
    Console.Out.Write(HelpText.WithQuotesOutsidePlaceholders(output.ToString()));

    if (parseResult.Action is HelpAction
        && HelpText.ExtendedHelpFor(parseResult.CommandResult.Command.Name) is { } extended)
    {
        Console.Out.WriteLine(extended);
        Console.Out.WriteLine();
    }
}
else
{
    exitCode = await parseResult.InvokeAsync(configuration).ConfigureAwait(false);
}

// Exit codes follow grep: 0 found, 1 nothing found, 2 something wrong with the request.
// System.CommandLine reports its own parse failures as 1, which would tell a script "no
// matches" when the command was actually malformed.
return parseResult.Errors.Count > 0 ? 2 : exitCode;
