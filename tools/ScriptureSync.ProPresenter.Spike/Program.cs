using ScriptureSync.ProPresenter;

if (args.Length != 2 || args[0] != "--address" || !Uri.TryCreate(args[1], UriKind.Absolute, out var address))
{
    Console.WriteLine("Usage: ScriptureSync.ProPresenter.Spike --address http://HOST:PORT");
    return 2;
}

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
using var handler = new HttpClientHandler { AllowAutoRedirect = false };
using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
try
{
    var client = new ProPresenterApiClient(http, address);
    var version = await client.GetVersionAsync(cancellation.Token);
    Console.WriteLine($"ProPresenter: Connected\nVersion: {version.Description}\nAPI: {version.ApiVersion}\nPlatform: {version.Platform}");
    Console.WriteLine("\nLibraries:");
    foreach (var library in await client.GetLibrariesAsync(cancellation.Token))
    {
        Console.WriteLine($" - {library.Name} [{library.Id}]");
        foreach (var presentation in await client.GetPresentationsAsync(library.Id, cancellation.Token))
            Console.WriteLine($"   Presentation: {presentation.Name} [{presentation.Id}]");
    }
    Console.WriteLine("\nPlaylists:");
    foreach (var playlist in await client.GetPlaylistsAsync(cancellation.Token))
    {
        var contents = await client.GetPlaylistContentsAsync(playlist.Id, cancellation.Token);
        var items = contents.GetProperty("items");
        Console.WriteLine($" - {playlist.Name} [{playlist.Id}]: {items.GetArrayLength()} items");
        // Inspect identity and item type without dumping slide text or arbitrary metadata.
        foreach (var item in items.EnumerateArray())
        {
            if (item.TryGetProperty("id", out var id)) Console.WriteLine($"   Item identity: {id}");
            if (item.TryGetProperty("type", out var type)) Console.WriteLine($"   Type: {type}");
        }
    }
    Console.WriteLine("\nRead-only enumeration complete. No content was changed or triggered.");
    return 0;
}
catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
{
    Console.Error.WriteLine("Diagnostic cancelled.");
    return 130;
}
catch (Exception exception) when (exception is ProPresenterDiagnosticException or ArgumentException)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}
