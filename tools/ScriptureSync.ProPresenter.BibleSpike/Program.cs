using ScriptureSync.Core.Parsing;
using ScriptureSync.ProPresenter;

if (args.Length != 0 && (args.Length != 2 || args[0] != "--bible-root"))
{
    Console.Error.WriteLine("Usage: ScriptureSync.ProPresenter.BibleSpike [--bible-root DIRECTORY]");
    return 2;
}
try
{
    var catalog = new ProPresenterBibleCatalog(args.Length == 2 ? args[1] : ProPresenterBibleCatalog.DefaultRoot);
    foreach (var installed in catalog.Discover()) Console.WriteLine($"Installed: {installed.Code} — {installed.Name}");
    var bible = catalog.Find("KJV");
    var reader = new UsxBibleReader();
    var parser = new ScriptureReferenceParser("KJV");
    var cases = new (string Reference, int Count)[]
    {
        ("John 3:16", 1), ("Psalm 23:1-4", 4), ("1 Corinthians 13:4-7", 4),
        ("1 John 4:8", 1), ("Song of Solomon 2:1", 1), ("Psalm 119:105", 1),
        ("Jude 1:24-25", 2), ("John 3:16-4:2", 23), ("Psalm 23", 6),
        ("Song of Songs 2:1", 1), ("1 Jn 4:7-10", 4), ("John 3:16,18", 2)
    };
    foreach (var test in cases)
    {
        var parsed = parser.Parse(test.Reference);
        if (!parsed.IsValid) throw new InvalidDataException($"Parser rejected {test.Reference}");
        var result = reader.Read(bible, parsed.Passages.Single());
        if (result.Verses.Count != test.Count) throw new InvalidDataException($"Unexpected verse count for {test.Reference}");
        Console.WriteLine($"PASS {test.Reference} ({result.Translation}): {result.Verses.Count} ordered, nonempty verses");
    }
    Console.WriteLine("Read-only passage checks passed. Bible text was not printed or saved.");
    return 0;
}
catch (Exception exception) when (exception is InvalidDataException or IOException or System.Xml.XmlException or UnauthorizedAccessException or FormatException)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}
