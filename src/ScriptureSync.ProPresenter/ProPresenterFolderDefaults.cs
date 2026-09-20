namespace ScriptureSync.ProPresenter;

public static class ProPresenterFolderDefaults
{
    public static string LibraryRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RenewedVision", "ProPresenter", "LocalWorkspaces", "ProPresenter", "Libraries");

    public static string BibleDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "RenewedVision", "ProPresenter", "Bibles");

    public static string? FindLibrary(string name, string address, string? libraryRoot = null)
    {
        // Local folder defaults cannot identify a remote server's library or a custom workspace.
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) || !uri.IsLoopback ||
            (uri.Scheme != "http" && uri.Scheme != "https") ||
            string.IsNullOrWhiteSpace(name) || name is "." or ".." ||
            name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return null;
        var candidate = Path.Combine(libraryRoot ?? LibraryRoot, name);
        return Directory.Exists(candidate) ? candidate : null;
    }
}
