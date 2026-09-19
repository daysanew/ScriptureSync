using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ScriptureSync.Core.Models;

namespace ScriptureSync.ProPresenter.BibleSpike;

public sealed record VerseText(string Book, int Chapter, int Verse, string Text);
public sealed record PassageText(string Translation, PassageReference Reference, IReadOnlyList<VerseText> Verses);

public sealed class UsxBibleReader
{
    private static readonly string[] Names = "Genesis|Exodus|Leviticus|Numbers|Deuteronomy|Joshua|Judges|Ruth|1 Samuel|2 Samuel|1 Kings|2 Kings|1 Chronicles|2 Chronicles|Ezra|Nehemiah|Esther|Job|Psalm|Proverbs|Ecclesiastes|Song of Solomon|Isaiah|Jeremiah|Lamentations|Ezekiel|Daniel|Hosea|Joel|Amos|Obadiah|Jonah|Micah|Nahum|Habakkuk|Zephaniah|Haggai|Zechariah|Malachi|Matthew|Mark|Luke|John|Acts|Romans|1 Corinthians|2 Corinthians|Galatians|Ephesians|Philippians|Colossians|1 Thessalonians|2 Thessalonians|1 Timothy|2 Timothy|Titus|Philemon|Hebrews|James|1 Peter|2 Peter|1 John|2 John|3 John|Jude|Revelation".Split('|');
    private static readonly string[] Codes = "GEN EXO LEV NUM DEU JOS JDG RUT 1SA 2SA 1KI 2KI 1CH 2CH EZR NEH EST JOB PSA PRO ECC SNG ISA JER LAM EZK DAN HOS JOL AMO OBA JON MIC NAM HAB ZEP HAG ZEC MAL MAT MRK LUK JHN ACT ROM 1CO 2CO GAL EPH PHP COL 1TH 2TH 1TI 2TI TIT PHM HEB JAS 1PE 2PE 1JN 2JN 3JN JUD REV".Split(' ');

    public PassageText Read(InstalledBible bible, PassageReference reference)
    {
        var index = Array.IndexOf(Names, reference.Book);
        if (index < 0) throw new InvalidDataException($"Unsupported canonical book: {reference.Book}");
        var path = Path.Combine(bible.PackageDirectory, "Release", "USX_1", Codes[index] + ".usx");
        if (!File.Exists(path)) throw new InvalidDataException($"Book {reference.Book} is missing or package layout is unsupported: {path}");
        var all = ReadBook(path, reference.Book, Codes[index]);
        var selected = new List<VerseText>();
        var endChapter = reference.EndChapter ?? reference.Chapter;
        if (reference.Chapter < 1 || endChapter < reference.Chapter)
            throw new InvalidDataException("Invalid chapter range.");
        for (var chapter = reference.Chapter; chapter <= endChapter; chapter++)
        {
            var available = all.Where(v => v.Chapter == chapter).ToDictionary(v => v.Verse);
            if (available.Count == 0) throw new InvalidDataException($"Chapter not found: {reference.Book} {chapter}");
            var numbers = new SortedSet<int>();
            if (reference.EndChapter is not null)
            {
                var start = chapter == reference.Chapter ? int.Parse(reference.VerseSelection!) : 1;
                var end = chapter == endChapter ? reference.EndVerse!.Value : available.Keys.Max();
                AddRange(numbers, start, end);
            }
            else if (string.IsNullOrEmpty(reference.VerseSelection))
                AddRange(numbers, 1, available.Keys.Max());
            else foreach (var part in reference.VerseSelection.Split(','))
            {
                var range = part.Split('-');
                AddRange(numbers, int.Parse(range[0]), int.Parse(range[^1]));
            }
            foreach (var number in numbers)
            {
                if (!available.TryGetValue(number, out var verse))
                    throw new InvalidDataException($"Verse not found: {reference.Book} {chapter}:{number}");
                selected.Add(verse);
            }
        }
        return new(bible.Code, reference, selected);
    }

    private static void AddRange(SortedSet<int> target, int start, int end)
    {
        if (start < 1 || end < start || end > 1000) throw new InvalidDataException("Invalid or unsupported verse range.");
        for (var number = start; number <= end; number++) target.Add(number);
    }

    public IReadOnlyList<VerseText> ReadBook(string path, string book, string code)
    {
        var root = ProPresenterBibleCatalog.ReadXml(path).Root;
        if (root?.Name != "usx" || (string?)root.Attribute("version") != "3.0" ||
            (string?)root.Element("book")?.Attribute("code") != code)
            throw new InvalidDataException($"Unsupported USX version or mismatched book in {path}");
        var result = new List<VerseText>();
        var seen = new HashSet<(int, int)>();
        var chapter = 0;
        string? activeChapter = null;
        var verse = 0;
        string? active = null;
        var text = new StringBuilder();

        void Visit(XNode node)
        {
            if (node is XText literal) { if (active is not null) text.Append(literal.Value); return; }
            if (node is not XElement element) return;
            var name = element.Name.LocalName;
            if (name is "book" or "note" or "figure") return;
            if (name == "char" && (string?)element.Attribute("style") is "va" or "vp") return;
            if (name == "chapter")
            {
                if (active is not null) throw new InvalidDataException("Unclosed verse at chapter boundary.");
                if (element.Attribute("eid") is { } chapterEnd)
                {
                    if (activeChapter != chapterEnd.Value) throw new InvalidDataException("Mismatched chapter end milestone.");
                    activeChapter = null;
                    chapter = 0;
                }
                else
                {
                    if (activeChapter is not null || !int.TryParse((string?)element.Attribute("number"), out chapter) || chapter < 1)
                        throw new InvalidDataException("Invalid or unclosed chapter start milestone.");
                    activeChapter = (string?)element.Attribute("sid");
                    if (activeChapter != $"{code} {chapter}") throw new InvalidDataException("Mismatched chapter start milestone.");
                }
                return;
            }
            if (name == "verse")
            {
                if (element.Attribute("eid") is { } end)
                {
                    if (active != end.Value) throw new InvalidDataException("Mismatched verse end milestone.");
                    var normalized = Regex.Replace(text.ToString(), @"\s+", " ").Trim();
                    if (normalized.Length == 0 || !seen.Add((chapter, verse))) throw new InvalidDataException("Empty or duplicate verse.");
                    result.Add(new(book, chapter, verse, normalized));
                    active = null;
                    text.Clear();
                }
                else
                {
                    if (active is not null || chapter < 1 || !int.TryParse((string?)element.Attribute("number"), out verse) || verse < 1)
                        throw new InvalidDataException("Invalid verse start; verse bridges/suffixes are unsupported in this spike.");
                    active = (string?)element.Attribute("sid");
                    if (active != $"{code} {chapter}:{verse}") throw new InvalidDataException("Mismatched verse start milestone.");
                }
                return;
            }
            if (name == "para")
            {
                var style = (string?)element.Attribute("style") ?? "";
                if (style == "iex" && !element.Descendants("verse").Any()) return;
                if (!Regex.IsMatch(style, @"^(p|m|q[1-4]?|pi[1-4]?|li[1-4]?|nb|b|pc|mi|pm|pmc|pmo|pmr|qr|qc|qm[1-4]?|cls|iex)$"))
                {
                    if (element.Descendants("verse").Any()) throw new InvalidDataException($"Verse in unsupported paragraph style: {style}");
                    return;
                }
                if (active is not null) text.Append(' ');
            }
            foreach (var child in element.Nodes()) Visit(child);
        }
        Visit(root);
        if (active is not null) throw new InvalidDataException("Unclosed verse at end of book.");
        if (activeChapter is not null) throw new InvalidDataException("Unclosed chapter at end of book.");
        return result.OrderBy(v => v.Chapter).ThenBy(v => v.Verse).ToArray();
    }
}
