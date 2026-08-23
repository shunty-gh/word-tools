using System.CommandLine;
using Words.Core;

namespace Words.Cli;

/// <summary>
/// <c>words licence</c> — reproduces this program's terms and those of the bundled word
/// lists.
/// </summary>
/// <remarks>
/// A release requirement rather than a courtesy: Apache 2.0 requires a copy of the licence
/// to reach whoever the program is distributed to, and both word lists require their notice
/// to accompany anything distributed. For a self-contained binary this command is the only
/// thing that can satisfy either.
/// </remarks>
internal static class LicenceCommand
{
    public static Command Create()
    {
        var command = new Command("licence", "Show this program's licence and those of the bundled word lists.");

        // The word lists themselves are American and British in origin; accept either
        // spelling rather than make anyone guess which was used.
        command.Aliases.Add("license");

        command.SetAction(_ =>
        {
            // The program's own terms come first: they are what a reader asking "can I use
            // this?" wants, and the word-list notices are the answer to a narrower question.
            Write(Licences.Program);

            Console.WriteLine();
            Console.WriteLine("The word lists bundled with this program are used under the following terms.");

            foreach (var licence in Licences.WordLists)
            {
                Console.WriteLine();
                Write(licence);
            }

            return ExitCodes.Found;
        });

        return command;
    }

    private static void Write(Licence licence)
    {
        Console.WriteLine(licence.Source);
        Console.WriteLine(new string('=', licence.Source.Length));
        Console.WriteLine();
        Console.WriteLine(licence.Summary);
        Console.WriteLine();
        Console.WriteLine(licence.Text);
    }
}
