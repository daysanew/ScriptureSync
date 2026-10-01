namespace ScriptureSync.Core.Configuration;

public sealed record ProPresenterConfiguration
{
    public string Address { get; init; } = "http://127.0.0.1:1025";
    public string BibleDirectory { get; init; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "RenewedVision", "ProPresenter", "Bibles");
    public string TemplatePath { get; init; } = "";
    public string LibraryDirectory { get; init; } = "";
    public string LibraryId { get; init; } = "";
    public string LibraryName { get; init; } = "";
    public string PlaylistId { get; init; } = "";
    public string PlaylistName { get; init; } = "";
}
