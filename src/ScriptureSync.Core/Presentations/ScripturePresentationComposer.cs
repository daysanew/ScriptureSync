using ScriptureSync.Core.Bibles;

namespace ScriptureSync.Core.Presentations;

/// <summary>
/// Converts resolved verses to logical slides without I/O, text reflow, or
/// destination-specific formatting. One passage/translation produces one presentation.
/// </summary>
public sealed class ScripturePresentationComposer
{
    public ScripturePresentation Compose(
        PassageText passage,
        SlideBreakPolicy policy = SlideBreakPolicy.OneVersePerSlide)
    {
        ArgumentNullException.ThrowIfNull(passage);
        if (policy != SlideBreakPolicy.OneVersePerSlide)
            throw new ArgumentOutOfRangeException(nameof(policy), policy, "Unsupported slide break policy.");
        ArgumentException.ThrowIfNullOrWhiteSpace(passage.Translation);
        ArgumentNullException.ThrowIfNull(passage.Reference);
        ArgumentException.ThrowIfNullOrWhiteSpace(passage.Reference.Book);
        ArgumentNullException.ThrowIfNull(passage.Verses);
        if (passage.Verses.Count == 0)
            throw new ArgumentException("Cannot compose a presentation without resolved verses.", nameof(passage));

        var slides = new List<ScriptureSlide>(passage.Verses.Count);
        VerseText? previous = null;
        foreach (var verse in passage.Verses)
        {
            if (verse is null || string.IsNullOrWhiteSpace(verse.Text) ||
                verse.Book != passage.Reference.Book || verse.Chapter < 1 || verse.Verse < 1)
                throw new ArgumentException("Each verse must contain text, match the passage book, and have positive chapter/verse numbers.", nameof(passage));
            if (previous is not null && (verse.Chapter < previous.Chapter ||
                (verse.Chapter == previous.Chapter && verse.Verse <= previous.Verse)))
                throw new ArgumentException("Resolved verses must be unique and in scripture order.", nameof(passage));

            slides.Add(new ScriptureSlide(
                $"{verse.Book} {verse.Chapter}:{verse.Verse}", verse.Text, passage.Translation));
            previous = verse;
        }

        // Snapshot the result so later changes to a provider's collection cannot alter a preview.
        return new ScripturePresentation($"{passage.Reference} ({passage.Translation})", slides.AsReadOnly());
    }
}
