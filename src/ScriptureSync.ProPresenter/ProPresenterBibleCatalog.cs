using System.Xml;
using System.Xml.Linq;

namespace ScriptureSync.ProPresenter;

public sealed record InstalledBible(string Code, string Name, string PackageDirectory);

public sealed class ProPresenterBibleCatalog(string root)
{
    public static string DefaultRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "RenewedVision", "ProPresenter", "Bibles");

    public IReadOnlyList<InstalledBible> Discover()
    {
        if (!Directory.Exists(root)) throw new InvalidDataException($"Bible directory not found: {root}");
        var bibles = new List<InstalledBible>();
        foreach (var directory in Directory.EnumerateDirectories(root).Order())
        {
            var metadata = Path.Combine(directory, "rvmetadata.xml");
            if (!File.Exists(metadata)) continue;
            var xml = ReadXml(metadata).Root;
            var code = xml?.Element("abbreviation")?.Value.Trim();
            var name = xml?.Element("name")?.Value.Trim();
            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
                throw new InvalidDataException($"Missing name/abbreviation in {metadata}");
            if (bibles.Any(b => b.Code.Equals(code, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException($"Ambiguous installed translation: {code}");
            bibles.Add(new(code, name, directory));
        }
        return bibles;
    }

    public InstalledBible Find(string code) => Discover().SingleOrDefault(b => b.Code.Equals(code, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidDataException($"Translation not installed: {code}");

    internal static XDocument ReadXml(string path)
    {
        // Never open installed packages for writing; prohibit external entities and DTDs.
        using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        return XDocument.Load(reader, LoadOptions.PreserveWhitespace);
    }
}
