namespace ScriptureSync.Core.Bibles;

/// <summary>A resolved verse. Text retains the provider's content and whitespace.</summary>
public sealed record VerseText(string Book, int Chapter, int Verse, string Text);
