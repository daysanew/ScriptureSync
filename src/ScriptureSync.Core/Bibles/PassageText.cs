using ScriptureSync.Core.Models;

namespace ScriptureSync.Core.Bibles;

/// <summary>
/// Resolved text for one passage and translation. Providers supply verses in
/// scripture order and are responsible for resolving the requested selection.
/// </summary>
public sealed record PassageText(string Translation, PassageReference Reference, IReadOnlyList<VerseText> Verses);
