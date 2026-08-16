using Words.Core;

namespace Words.Cli;

/// <summary>
/// Personal words in a plain text file, one entry per line.
/// </summary>
/// <remarks>
/// Plain text rather than a database on purpose: it is diffable, hand-editable, and syncs
/// through a repository or a synced folder without us building anything. A remote store
/// can replace this later by implementing the same interface.
/// </remarks>
internal sealed class FilePersonalWordStore(string path) : IPersonalWordStore
{
    public string Path { get; } = path ?? throw new ArgumentNullException(nameof(path));

    /// <summary>
    /// Where personal words live by default:
    /// <c>~/Library/Application Support/words/personal.txt</c> on macOS,
    /// <c>~/.config/words/personal.txt</c> on Linux,
    /// <c>%APPDATA%\words\personal.txt</c> on Windows.
    /// </summary>
    public static string DefaultPath => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "words",
        "personal.txt");

    public async ValueTask<IReadOnlyList<string>> ReadLinesAsync(CancellationToken cancellationToken = default) =>
        File.Exists(Path)
            ? await File.ReadAllLinesAsync(Path, cancellationToken).ConfigureAwait(false)
            : [];

    public async ValueTask AddAsync(string displayForm, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayForm);

        var directory = System.IO.Path.GetDirectoryName(Path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.AppendAllTextAsync(
            Path,
            displayForm.Trim() + Environment.NewLine,
            cancellationToken).ConfigureAwait(false);
    }
}
