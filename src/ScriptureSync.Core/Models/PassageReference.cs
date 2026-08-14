namespace ScriptureSync.Core.Models;

public sealed record PassageReference(
    string Book,
    int Chapter,
    string? VerseSelection = null,
    int? EndChapter = null,
    int? EndVerse = null)
{
    public override string ToString()
    {
        if (EndChapter is not null && EndVerse is not null)
        {
            return $"{Book} {Chapter}:{VerseSelection}-{EndChapter}:{EndVerse}";
        }

        return string.IsNullOrWhiteSpace(VerseSelection)
            ? $"{Book} {Chapter}"
            : $"{Book} {Chapter}:{VerseSelection}";
    }
}
