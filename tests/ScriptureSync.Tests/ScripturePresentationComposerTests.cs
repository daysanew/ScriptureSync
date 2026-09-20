using ScriptureSync.Core.Bibles;
using ScriptureSync.Core.Models;
using ScriptureSync.Core.Parsing;
using ScriptureSync.Core.Presentations;

namespace ScriptureSync.Tests;

public sealed class ScripturePresentationComposerTests
{
    private readonly ScripturePresentationComposer _composer = new();

    [Theory]
    [InlineData("John 3:16 (KJV)", "John", 3, 16, 1)]
    [InlineData("John 3:16-18 (KJV)", "John", 3, 16, 3)]
    [InlineData("Psalm 23 (KJV)", "Psalm", 23, 1, 6)]
    [InlineData("Psalm 23:1-6 (KJV)", "Psalm", 23, 1, 6)]
    [InlineData("1 Corinthians 13:4-7 (KJV)", "1 Corinthians", 13, 4, 4)]
    [InlineData("1 Jn 4:7-10 (KJV)", "1 John", 4, 7, 4)]
    [InlineData("Song of Songs 2:1 (KJV)", "Song of Solomon", 2, 1, 1)]
    [InlineData("Jude 1:24-25 (KJV)", "Jude", 1, 24, 2)]
    public void Parser_to_slides_preserves_resolved_text_and_canonical_reference(
        string input, string book, int chapter, int firstVerse, int count)
    {
        var parsed = new ScriptureReferenceParser().Parse(input);
        Assert.True(parsed.IsValid);
        var reference = Assert.Single(parsed.Passages);
        // Stand-in for a text provider; no third-party Bible text is included.
        var verses = Enumerable.Range(firstVerse, count)
            .Select(number => new VerseText(book, chapter, number, $"Synthetic text {number}."))
            .ToArray();

        var result = _composer.Compose(new(Assert.Single(parsed.TranslationCodes), reference, verses));

        Assert.Equal($"{reference} (KJV)", result.Title);
        Assert.Equal(count, result.Slides.Count);
        Assert.Equal(verses.Select(v => v.Text), result.Slides.Select(s => s.Text));
        Assert.Equal(verses.Select(v => $"{book} {chapter}:{v.Verse}"), result.Slides.Select(s => s.Reference));
        Assert.All(result.Slides, slide => Assert.Equal("KJV", slide.Translation));
    }

    [Fact]
    public void Cross_chapter_range_preserves_boundary_references()
    {
        var reference = Assert.Single(new ScriptureReferenceParser().Parse("John 3:16-4:2").Passages);
        var verses = Enumerable.Range(16, 21).Select(n => new VerseText("John", 3, n, $"Chapter three {n}."))
            .Concat([new("John", 4, 1, "Chapter four first."), new("John", 4, 2, "Chapter four second.")]).ToArray();

        var result = _composer.Compose(new("KJV", reference, verses));

        Assert.Equal(23, result.Slides.Count);
        Assert.Equal("John 3:16", result.Slides[0].Reference);
        Assert.Equal("John 3:36", result.Slides[20].Reference);
        Assert.Equal("John 4:1", result.Slides[21].Reference);
        Assert.Equal("John 4:2", result.Slides[22].Reference);
        Assert.Equal("John 3:16-4:2 (KJV)", result.Title);
    }

    [Fact]
    public void Multiple_translations_compose_separate_presentations_with_their_own_text()
    {
        var parsed = new ScriptureReferenceParser().Parse("John 3:16 (KJV & NLT)");
        var reference = Assert.Single(parsed.Passages);
        var results = parsed.TranslationCodes.Select(code => _composer.Compose(
            new PassageText(code, reference, [new("John", 3, 16, $"Synthetic {code} text.")]))).ToArray();

        Assert.Equal(["John 3:16 (KJV)", "John 3:16 (NLT)"], results.Select(p => p.Title));
        Assert.Equal(["Synthetic KJV text.", "Synthetic NLT text."], results.Select(p => Assert.Single(p.Slides).Text));
        Assert.Equal(["KJV", "NLT"], results.Select(p => Assert.Single(p.Slides).Translation));
    }

    [Fact]
    public void Long_unicode_text_and_line_breaks_remain_one_unmodified_slide()
    {
        var text = "  Synthetic — café\n" + new string('x', 10000) + "\r\nEnd.  ";
        var result = _composer.Compose(new("TEST", new("Psalm", 119, "105"), [new("Psalm", 119, 105, text)]));
        Assert.Equal(new ScriptureSlide("Psalm 119:105", text, "TEST"), Assert.Single(result.Slides));
    }

    [Fact]
    public void Discontiguous_selection_does_not_invent_intervening_verses()
    {
        var reference = Assert.Single(new ScriptureReferenceParser().Parse("John 3:16,18").Passages);
        var result = _composer.Compose(new("KJV", reference, [new("John", 3, 16, "First."), new("John", 3, 18, "Last.")]));
        Assert.Equal(["John 3:16", "John 3:18"], result.Slides.Select(s => s.Reference));
    }

    [Fact]
    public void Composition_is_repeatable_and_detached_from_mutable_provider_collections()
    {
        var verses = new List<VerseText> { new("John", 3, 16, "Synthetic.") };
        var passage = new PassageText("KJV", new("John", 3, "16"), verses);
        var first = _composer.Compose(passage);
        var second = _composer.Compose(passage);
        Assert.Equal(first.Title, second.Title);
        Assert.Equal(first.Slides.ToArray(), second.Slides.ToArray());
        verses.Clear();
        Assert.Single(first.Slides);
        Assert.Throws<NotSupportedException>(() => ((IList<ScriptureSlide>)first.Slides).Clear());
    }

    [Fact]
    public void Empty_resolution_and_unsupported_policy_fail_explicitly()
    {
        Assert.Throws<ArgumentException>(() => _composer.Compose(new("KJV", new("John", 3), [])));
        Assert.Throws<ArgumentOutOfRangeException>(() => _composer.Compose(
            new("KJV", new("John", 3), [new("John", 3, 1, "Synthetic.")]), (SlideBreakPolicy)99));
    }

    [Theory]
    [InlineData("Romans", 3, 16, "Wrong book")]
    [InlineData("John", 0, 16, "Invalid chapter")]
    [InlineData("John", 3, 0, "Invalid verse")]
    [InlineData("John", 3, 16, " ")]
    public void Invalid_provider_verses_are_rejected(string book, int chapter, int verse, string text)
    {
        Assert.Throws<ArgumentException>(() => _composer.Compose(
            new("KJV", new("John", 3, "16"), [new(book, chapter, verse, text)])));
    }

    [Theory]
    [InlineData(16)]
    [InlineData(15)]
    public void Duplicate_or_reversed_verses_are_rejected_without_silently_reordering(int lastVerse)
    {
        Assert.Throws<ArgumentException>(() => _composer.Compose(new("KJV", new("John", 3),
            [new("John", 3, 16, "First."), new("John", 3, lastVerse, "Second.")])));
    }
}
