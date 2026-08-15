namespace Words.Lexicon.Building;

/// <summary>
/// Reads one format of word list. Readers identify their own files by content rather than
/// by name, so the builder can be pointed at a directory and work out what is in it.
/// </summary>
public interface IWordListReader
{
    /// <summary>Name recorded in the manifest for files this reader handled.</summary>
    string Name { get; }

    /// <summary>Whether this reader recognises the file, judged from its opening lines.</summary>
    bool CanRead(IReadOnlyList<string> sampleLines);

    /// <summary>Reads every entry, mapping the list's own scoring onto the shared scale.</summary>
    IEnumerable<RawEntry> Read(TextReader reader);
}
