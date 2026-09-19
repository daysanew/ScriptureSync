using ScriptureSync.Core.Parsing;
using ScriptureSync.ProPresenter.BibleSpike;

namespace ScriptureSync.Tests;

public sealed class ProPresenterBibleTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ScriptureSyncBibleTests-" + Guid.NewGuid().ToString("N"));
    // Deliberately synthetic text; no installed Bible content is redistributed.
    private const string Book = """
        <usx version="3.0"><book code="JHN"/><chapter number="3" sid="JHN 3"/>
        <para style="s1">Heading excluded</para>
        <para style="p"><verse number="1" sid="JHN 3:1"/>Alpha <char style="wj">beta</char><note>Excluded note</note>.</para>
        <para style="q1" vid="JHN 3:1">Gamma.<verse eid="JHN 3:1"/>
        <verse number="2" sid="JHN 3:2"/>Second.<verse eid="JHN 3:2"/></para>
        <para style="iex"><verse number="3" sid="JHN 3:3"/>Third.<verse eid="JHN 3:3"/></para>
        <chapter eid="JHN 3"/><chapter number="4" sid="JHN 4"/>
        <para style="p"><verse number="1" sid="JHN 4:1"/>Fourth.<verse eid="JHN 4:1"/></para>
        <chapter eid="JHN 4"/></usx>
        """;

    private InstalledBible Install(string code = "TEST", string? xml = null)
    {
        var package = Path.Combine(_root, Guid.NewGuid().ToString());
        Directory.CreateDirectory(Path.Combine(package, "Release", "USX_1"));
        File.WriteAllText(Path.Combine(package, "rvmetadata.xml"),
            $"<RVBibleMetdata><name>Synthetic translation</name><abbreviation>{code}</abbreviation></RVBibleMetdata>");
        File.WriteAllText(Path.Combine(package, "Release", "USX_1", "JHN.usx"), xml ?? Book);
        return new(code, "Synthetic translation", package);
    }

    [Fact]
    public void Catalog_maps_multiple_codes_case_insensitively_and_rejects_ambiguity()
    {
        var first = Install();
        Install("OTHER");
        var catalog = new ProPresenterBibleCatalog(_root);
        Assert.Equal(2, catalog.Discover().Count);
        Assert.Equal(first, catalog.Find("test"));
        Assert.Contains("Translation not installed", Assert.Throws<InvalidDataException>(() => catalog.Find("ABSENT")).Message);
        Install("test");
        Assert.Contains("Ambiguous", Assert.Throws<InvalidDataException>(() => catalog.Discover()).Message);
    }

    [Theory]
    [InlineData("John 3", 3)]
    [InlineData("Jn 3:1,3", 2)]
    [InlineData("John 3:2-4:1", 3)]
    public void Selects_structured_passages_in_order(string reference, int expected)
    {
        var bible = Install();
        var parsed = new ScriptureReferenceParser().Parse(reference);
        var result = new UsxBibleReader().Read(bible, Assert.Single(parsed.Passages));
        Assert.Equal(expected, result.Verses.Count);
        Assert.Equal("TEST", result.Translation);
        Assert.Equal(result.Verses.OrderBy(v => v.Chapter).ThenBy(v => v.Verse), result.Verses);
    }

    [Fact]
    public void Preserves_inline_text_and_paragraph_spaces_without_headings_or_notes()
    {
        var bible = Install();
        var before = File.ReadAllBytes(Path.Combine(bible.PackageDirectory, "Release", "USX_1", "JHN.usx"));
        var result = new UsxBibleReader().Read(bible, new("John", 3, "1"));
        Assert.Equal("Alpha beta. Gamma.", Assert.Single(result.Verses).Text);
        Assert.Equal(before, File.ReadAllBytes(Path.Combine(bible.PackageDirectory, "Release", "USX_1", "JHN.usx")));
    }

    [Theory]
    [InlineData("John 99:1", "Chapter not found")]
    [InlineData("John 3:99", "Verse not found")]
    [InlineData("Genesis 1:1", "Book Genesis is missing")]
    public void Missing_data_is_explicit(string reference, string diagnostic)
    {
        var bible = Install();
        var parsed = new ScriptureReferenceParser().Parse(reference);
        Assert.Contains(diagnostic, Assert.Throws<InvalidDataException>(() =>
            new UsxBibleReader().Read(bible, parsed.Passages.Single())).Message);
    }

    [Theory]
    [InlineData("version=\"3.0\"", "version=\"2.5\"")]
    [InlineData("eid=\"JHN 3:1\"", "eid=\"JHN 3:9\"")]
    [InlineData("<chapter eid=\"JHN 3\"/>", "")]
    [InlineData("<chapter eid=\"JHN 4\"/>", "<chapter eid=\"JHN 99\"/>")]
    [InlineData("number=\"1\" sid=\"JHN 3:1\"", "number=\"1-2\" sid=\"JHN 3:1-2\"")]
    public void Unsupported_or_malformed_milestones_are_not_silently_accepted(string from, string to)
    {
        var bible = Install(xml: Book.Replace(from, to));
        Assert.Throws<InvalidDataException>(() => new UsxBibleReader().Read(bible, new("John", 3)));
    }

    [Fact]
    public void Prohibits_dtds_and_rejects_malformed_metadata()
    {
        var bible = Install(xml: "<!DOCTYPE usx [<!ENTITY x 'oops'>]><usx>&x;</usx>");
        Assert.Throws<System.Xml.XmlException>(() => new UsxBibleReader().Read(bible, new("John", 3)));
        File.WriteAllText(Path.Combine(bible.PackageDirectory, "rvmetadata.xml"), "<RVBibleMetdata/>");
        Assert.Throws<InvalidDataException>(() => new ProPresenterBibleCatalog(_root).Discover());
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
