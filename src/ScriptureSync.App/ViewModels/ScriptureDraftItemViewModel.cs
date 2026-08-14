using ScriptureSync.Core.Parsing;

namespace ScriptureSync.App.ViewModels;

public sealed class ScriptureDraftItemViewModel : ObservableObject
{
    private readonly ScriptureReferenceParser _parser;
    private string _rawText;
    private string _status = string.Empty;
    private string _normalizedText = string.Empty;
    private bool _isValid;

    public ScriptureDraftItemViewModel(
        ScriptureReferenceParser parser,
        Guid id,
        string rawText,
        string source = "Manual")
    {
        _parser = parser;
        Id = id;
        Source = source;
        _rawText = rawText;
        Validate();
    }

    public Guid Id { get; }

    public string Source { get; }

    public string RawText
    {
        get => _rawText;
        set
        {
            if (SetProperty(ref _rawText, value))
            {
                Validate();
            }
        }
    }

    public bool IsValid
    {
        get => _isValid;
        private set => SetProperty(ref _isValid, value);
    }

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public string NormalizedText
    {
        get => _normalizedText;
        private set => SetProperty(ref _normalizedText, value);
    }

    public ScriptureParseResult ParseResult => _parser.Parse(RawText);

    public void SetSyncStatus(string status) => Status = status;

    private void Validate()
    {
        var result = _parser.Parse(RawText);
        IsValid = result.IsValid;
        Status = result.IsValid ? "Ready" : result.ErrorMessage;
        NormalizedText = result.IsValid
            ? $"{string.Join("; ", result.Passages)} ({string.Join(" & ", result.TranslationCodes)})"
            : string.Empty;
    }
}
