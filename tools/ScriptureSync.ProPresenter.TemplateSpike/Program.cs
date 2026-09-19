using Google.Protobuf;
using Pro.SerializationInterop.RVProtoData;
using ScriptureSync.Core.Parsing;
using ScriptureSync.ProPresenter;


if (args.Length != 3)
{
    Console.Error.WriteLine("Usage: TemplateSpike TEMPLATE.pro OUTPUT_DIRECTORY REFERENCE");
    return 2;
}
try
{
    var original = File.ReadAllBytes(args[0]);
    var template = Presentation.Parser.ParseFrom(original);
    Console.WriteLine($"Template: {template.Name}; {template.Cues.Count} cues");
    Console.WriteLine($"Original round-trip byte identical: {original.SequenceEqual(template.ToByteArray())}");
    var parsed = new ScriptureReferenceParser("KJV").Parse(args[2]);
    if (!parsed.IsValid || parsed.Passages.Count != 1 || parsed.TranslationCodes.Count != 1)
        throw new InvalidDataException("Supply one contiguous passage and one translation.");
    var reference = parsed.Passages[0];
    if (reference.VerseSelection?.Contains(',') == true) throw new InvalidDataException("Discontiguous ranges are outside this spike.");
    var bible = new ProPresenterBibleCatalog(ProPresenterBibleCatalog.DefaultRoot).Find(parsed.TranslationCodes[0]);
    var passage = new UsxBibleReader().Read(bible, reference);
    var output = ProPresenterDocumentWriter.Create(template, passage);
    var bytes = output.ToByteArray();
    if (!output.Equals(Presentation.Parser.ParseFrom(bytes))) throw new InvalidDataException("Output round-trip failed.");
    if (!original.SequenceEqual(template.ToByteArray())) throw new InvalidDataException("In-memory template changed.");
    var directory = Path.GetFullPath(args[1]);
    Directory.CreateDirectory(directory);
    var name = string.Concat(output.Name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
    var path = Path.Combine(directory, name + ".pro");
    using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write)) file.Write(bytes);
    Console.WriteLine($"Generated: {path}\nSlides: {output.Cues.Count}; new document UUID: {output.Uuid.String}");
    Console.WriteLine("Not imported. Manual rendering, editing, and save/reopen verification are required.");
    return 0;
}
catch (Exception exception) when (exception is InvalidDataException or IOException or Google.Protobuf.InvalidProtocolBufferException or UnauthorizedAccessException or System.Xml.XmlException or ArgumentException)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}
