using Words.Core;

namespace Words.Lexicon.Building;

/// <summary>
/// One entry as a word list stated it, before merging. The score has already been mapped
/// onto the shared 0–100 scale by the reader that produced it.
/// </summary>
public sealed record RawEntry(string DisplayForm, int Score, Sources Source, bool IsRacy);
