using System.CommandLine;
using System.Diagnostics;
using Words.Core;
using Words.LexiconBuilding;

namespace Words.Cli;

/// <summary>
/// <c>words lexicon build</c> — merges every word list in a directory into the committed
/// artefact. Brought forward from phase 6 because phase 1 has no other way to produce one.
/// </summary>
internal static class LexiconCommand
{
    public static Command Create()
    {
        var sourceDirectory = new Argument<DirectoryInfo>("source-directory")
        {
            Description = "Directory of word lists. Files that no reader recognises are skipped.",
        };

        var output = new Option<FileInfo>("--output", "-o")
        {
            Description = "Artefact to write. The manifest is written alongside it as <name>.manifest.json.",
            Required = true,
        };

        var build = new Command("build", "Merge word lists into a lexicon artefact.")
        {
            sourceDirectory,
            output,
        };

        build.SetAction((parseResult, cancellationToken) => BuildAsync(
            parseResult.GetValue(sourceDirectory)!,
            parseResult.GetValue(output)!,
            cancellationToken));

        var lexicon = new Command("lexicon", "Build and inspect the lexicon.");
        lexicon.Aliases.Add("lex");
        lexicon.Subcommands.Add(build);
        lexicon.Subcommands.Add(CreateInfo());
        return lexicon;
    }

    private static Command CreateInfo()
    {
        var info = new Command("info", "Load the lexicon and report what is in it, and how long it took.");

        info.SetAction(async (_, cancellationToken) =>
        {
            var stopwatch = Stopwatch.StartNew();
            var lexicon = await Composition.LoadLexiconAsync(cancellationToken).ConfigureAwait(false);
            var loadMs = stopwatch.ElapsedMilliseconds;

            // Both indexes are lazy, so touching each one separately reports what a query of
            // that kind would actually pay on a cold start.
            stopwatch.Restart();
            var lengths = lexicon.DistinctLengths;
            var lengthIndexMs = stopwatch.ElapsedMilliseconds;

            stopwatch.Restart();
            var anagramGroups = lexicon.DistinctCanonicalForms;
            var anagramIndexMs = stopwatch.ElapsedMilliseconds;

            var personal = lexicon.Entries.Count(e => e.Sources.HasFlag(Sources.Personal));

            Console.WriteLine($"  sources             {string.Join(", ", lexicon.SourceNames)}");
            Console.WriteLine($"  entries             {lexicon.Count,9:N0}");
            Console.WriteLine($"  single words        {lexicon.Entries.Count(e => e.Kinds.HasFlag(EntryKinds.SingleWord)),9:N0}");
            Console.WriteLine($"  phrases             {lexicon.Entries.Count(e => e.Kinds.HasFlag(EntryKinds.Phrase)),9:N0}");
            Console.WriteLine($"  proper nouns        {lexicon.Entries.Count(e => e.Kinds.HasFlag(EntryKinds.ProperNoun)),9:N0}");
            Console.WriteLine($"  racy                {lexicon.Entries.Count(e => e.IsRacy),9:N0}");
            Console.WriteLine($"  personal            {personal,9:N0}  ({Composition.PersonalWordsPath})");
            Console.WriteLine($"  distinct lengths    {lengths,9:N0}");
            Console.WriteLine($"  anagram groups      {anagramGroups,9:N0}");
            Console.WriteLine();
            Console.WriteLine($"  load                {loadMs,9:N0} ms   every query pays this");
            Console.WriteLine($"  + length index      {lengthIndexMs,9:N0} ms   pattern queries only");
            Console.WriteLine($"  + anagram index     {anagramIndexMs,9:N0} ms   anagram queries only");

            return 0;
        });

        return info;
    }

    private static async Task<int> BuildAsync(
        DirectoryInfo sourceDirectory,
        FileInfo output,
        CancellationToken cancellationToken)
    {
        if (!sourceDirectory.Exists)
        {
            Console.Error.WriteLine($"words: source directory not found: {sourceDirectory.FullName}");
            return 2;
        }

        Console.Error.WriteLine($"Reading word lists from {sourceDirectory.FullName}");

        var result = LexiconBuilder.Build(
            sourceDirectory.FullName,
            log: message => Console.Error.WriteLine(message));

        if (result.Entries.Count == 0)
        {
            Console.Error.WriteLine("words: no recognised word lists found; nothing to build.");
            return 2;
        }

        output.Directory?.Create();

        await using (var stream = output.Open(FileMode.Create, FileAccess.Write))
        {
            await LexiconArtefact.WriteAsync(stream, result.Entries, cancellationToken).ConfigureAwait(false);
        }

        var manifestPath = Path.ChangeExtension(output.FullName, null) + ".manifest.json";
        await File.WriteAllTextAsync(manifestPath, result.Manifest.ToJson(), cancellationToken).ConfigureAwait(false);

        var manifest = result.Manifest;
        Console.Error.WriteLine();
        Console.Error.WriteLine($"  merged        {manifest.EntryCount,9:N0} entries");
        Console.Error.WriteLine($"  single words  {manifest.SingleWordCount,9:N0}");
        Console.Error.WriteLine($"  phrases       {manifest.PhraseCount,9:N0}");
        Console.Error.WriteLine($"  proper nouns  {manifest.ProperNounCount,9:N0}");
        Console.Error.WriteLine($"  racy          {manifest.RacyCount,9:N0} (excluded from queries by default)");
        Console.Error.WriteLine($"  discarded     {manifest.DiscardedCount,9:N0} (no usable letters)");
        Console.Error.WriteLine();
        Console.Error.WriteLine($"  artefact  {output.FullName} ({new FileInfo(output.FullName).Length:N0} bytes)");
        Console.Error.WriteLine($"  manifest  {manifestPath}");

        return 0;
    }
}
