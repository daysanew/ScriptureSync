using System.Text.RegularExpressions;

namespace ScriptureSync.Core.Parsing;

internal static partial class BibleBookNames
{
    private static readonly IReadOnlyDictionary<string, string> Aliases = BuildAliases();

    public static bool TryRead(string text, out string canonicalBook, out string body)
    {
        canonicalBook = string.Empty;
        body = string.Empty;

        var match = BookPrefixRegex().Match(text.Trim());
        if (!match.Success)
        {
            return false;
        }

        var key = NormalizeAlias(match.Groups["book"].Value);
        if (!Aliases.TryGetValue(key, out canonicalBook!))
        {
            return false;
        }

        body = match.Groups["body"].Value.Trim();
        return true;
    }

    private static IReadOnlyDictionary<string, string> BuildAliases()
    {
        var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        Add(aliases, "Genesis", "Gen", "Ge", "Gn");
        Add(aliases, "Exodus", "Exod", "Exo", "Ex");
        Add(aliases, "Leviticus", "Lev", "Le", "Lv");
        Add(aliases, "Numbers", "Num", "Nu", "Nm", "Nb");
        Add(aliases, "Deuteronomy", "Deut", "Deu", "Dt");
        Add(aliases, "Joshua", "Josh", "Jos", "Jsh");
        Add(aliases, "Judges", "Judg", "Jdg", "Jg", "Jdgs");
        Add(aliases, "Ruth", "Rth", "Ru");
        AddNumbered(aliases, 1, "Samuel", "Sam", "Sa");
        AddNumbered(aliases, 2, "Samuel", "Sam", "Sa");
        AddNumbered(aliases, 1, "Kings", "Kgs", "Ki");
        AddNumbered(aliases, 2, "Kings", "Kgs", "Ki");
        AddNumbered(aliases, 1, "Chronicles", "Chron", "Chr", "Ch");
        AddNumbered(aliases, 2, "Chronicles", "Chron", "Chr", "Ch");
        Add(aliases, "Ezra", "Ezr");
        Add(aliases, "Nehemiah", "Neh", "Ne");
        Add(aliases, "Esther", "Esth", "Est", "Es");
        Add(aliases, "Job", "Jb");
        Add(aliases, "Psalm", "Psalms", "Ps", "Psa", "Psm", "Pss");
        Add(aliases, "Proverbs", "Prov", "Pro", "Prv", "Pr");
        Add(aliases, "Ecclesiastes", "Eccles", "Eccle", "Ecc", "Ec");
        Add(aliases, "Song of Solomon", "Song of Songs", "Song", "SOS", "Canticles", "Cant");
        Add(aliases, "Isaiah", "Isa", "Is");
        Add(aliases, "Jeremiah", "Jer", "Je", "Jr");
        Add(aliases, "Lamentations", "Lam", "La");
        Add(aliases, "Ezekiel", "Ezek", "Eze", "Ezk");
        Add(aliases, "Daniel", "Dan", "Da", "Dn");
        Add(aliases, "Hosea", "Hos", "Ho");
        Add(aliases, "Joel", "Joe", "Jl");
        Add(aliases, "Amos", "Am");
        Add(aliases, "Obadiah", "Obad", "Ob");
        Add(aliases, "Jonah", "Jon");
        Add(aliases, "Micah", "Mic", "Mc");
        Add(aliases, "Nahum", "Nah", "Na");
        Add(aliases, "Habakkuk", "Hab", "Hb");
        Add(aliases, "Zephaniah", "Zeph", "Zep", "Zp");
        Add(aliases, "Haggai", "Hag", "Hg");
        Add(aliases, "Zechariah", "Zech", "Zec", "Zc");
        Add(aliases, "Malachi", "Mal", "Ml");
        Add(aliases, "Matthew", "Matt", "Mt");
        Add(aliases, "Mark", "Mrk", "Mar", "Mk", "Mr");
        Add(aliases, "Luke", "Luk", "Lk");
        Add(aliases, "John", "Jn", "Jhn");
        Add(aliases, "Acts", "Act", "Ac");
        Add(aliases, "Romans", "Rom", "Ro", "Rm");
        AddNumbered(aliases, 1, "Corinthians", "Cor", "Co");
        AddNumbered(aliases, 2, "Corinthians", "Cor", "Co");
        Add(aliases, "Galatians", "Gal", "Ga");
        Add(aliases, "Ephesians", "Eph", "Ephes");
        Add(aliases, "Philippians", "Phil", "Php", "Pp");
        Add(aliases, "Colossians", "Col");
        AddNumbered(aliases, 1, "Thessalonians", "Thess", "Thes", "Th");
        AddNumbered(aliases, 2, "Thessalonians", "Thess", "Thes", "Th");
        AddNumbered(aliases, 1, "Timothy", "Tim", "Ti");
        AddNumbered(aliases, 2, "Timothy", "Tim", "Ti");
        Add(aliases, "Titus", "Tit");
        Add(aliases, "Philemon", "Philem", "Phm", "Pm");
        Add(aliases, "Hebrews", "Heb");
        Add(aliases, "James", "Jas", "Jm");
        AddNumbered(aliases, 1, "Peter", "Pet", "Pe", "Pt");
        AddNumbered(aliases, 2, "Peter", "Pet", "Pe", "Pt");
        AddNumbered(aliases, 1, "John", "Jn", "Jhn");
        AddNumbered(aliases, 2, "John", "Jn", "Jhn");
        AddNumbered(aliases, 3, "John", "Jn", "Jhn");
        Add(aliases, "Jude", "Jud");
        Add(aliases, "Revelation", "Rev", "Re", "Rv");

        return aliases;
    }

    private static void Add(
        IDictionary<string, string> aliases,
        string canonical,
        params string[] alternatives)
    {
        aliases[NormalizeAlias(canonical)] = canonical;
        foreach (var alternative in alternatives)
        {
            aliases[NormalizeAlias(alternative)] = canonical;
        }
    }

    private static void AddNumbered(
        IDictionary<string, string> aliases,
        int number,
        string name,
        params string[] abbreviations)
    {
        var canonical = $"{number} {name}";
        var prefixes = number switch
        {
            1 => new[] { "1", "I", "First" },
            2 => new[] { "2", "II", "Second" },
            3 => new[] { "3", "III", "Third" },
            _ => throw new ArgumentOutOfRangeException(nameof(number))
        };

        foreach (var prefix in prefixes)
        {
            Add(aliases, canonical, $"{prefix} {name}", $"{prefix}{name}");
            foreach (var abbreviation in abbreviations)
            {
                aliases[NormalizeAlias($"{prefix} {abbreviation}")] = canonical;
                aliases[NormalizeAlias($"{prefix}{abbreviation}")] = canonical;
            }
        }
    }

    private static string NormalizeAlias(string value) =>
        Regex.Replace(value, @"[\s.]", string.Empty).ToLowerInvariant();

    [GeneratedRegex(@"^(?<book>(?:(?:[1-3]|I{1,3}|First|Second|Third)\s*)?[A-Za-z.]+(?:\s+[A-Za-z.]+)*?)\s*(?<body>\d.*)$", RegexOptions.IgnoreCase)]
    private static partial Regex BookPrefixRegex();
}
