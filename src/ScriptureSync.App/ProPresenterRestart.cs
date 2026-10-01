using System.Diagnostics;
using System.IO;
using ScriptureSync.ProPresenter;

namespace ScriptureSync.App;

internal static class ProPresenterRestart
{
    public static async Task LinkAsync(string workspace, PcoPlaylist playlist, IReadOnlyList<PcoLinkRequest> requests,
        string backupDirectory, IProPresenterApiClient api, Action<string> status, CancellationToken token)
    {
        var processes = Process.GetProcessesByName("ProPresenter");
        if (processes.Length != 1)
        {
            foreach (var p in processes) p.Dispose();
            throw new InvalidOperationException("Open exactly one ProPresenter instance before syncing PCO links.");
        }
        using var process = processes[0];
        var executable = process.MainModule?.FileName ?? throw new InvalidOperationException("Cannot locate the running ProPresenter executable.");
        Directory.CreateDirectory(backupDirectory);
        var backup = Path.Combine(backupDirectory, $"native-playlists-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}.bak");
        var path = Path.Combine(workspace, "Playlists", "Library");
        // This lock coordinates ScriptureSync instances; ProPresenter itself does not honor it.
        using var syncLock = new FileStream(Path.Combine(workspace, "Playlists", "ScriptureSync.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var closeRequested = false;
        try
        {
            token.ThrowIfCancellationRequested();
            status("Closing ProPresenter normally. If ProPresenter asks to quit or save, respond there. No playlist files will be changed until it exits.");
            if (!process.CloseMainWindow()) throw new InvalidOperationException("ProPresenter could not be asked to close. No playlist files were changed.");
            closeRequested = true;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(60));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            { throw new InvalidOperationException("ProPresenter did not exit within a minute. Finish or cancel its quit dialog, then refresh preview. No playlist files were changed."); }
            token.ThrowIfCancellationRequested();
            EnsureClosed();
            var original = await File.ReadAllBytesAsync(path, token);
            var candidate = ProPresenterPcoSync.Prepare(original, workspace, playlist, requests);
            if (!candidate.SequenceEqual(original))
            {
                var temporary = path + ".ScriptureSync-" + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    await File.WriteAllBytesAsync(temporary, candidate, token);
                    token.ThrowIfCancellationRequested();
                    EnsureClosed();
                    if (!File.ReadAllBytes(path).SequenceEqual(original)) throw new InvalidOperationException("Playlist changed during sync. Nothing was replaced.");
                    File.Copy(path, backup, false);
                    File.Move(temporary, path, true);
                    if (!File.ReadAllBytes(path).SequenceEqual(candidate)) throw new IOException($"Playlist write could not be verified. Backup: {backup}");
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
        }
        finally
        {
            // Once we have closed the user's app, cancellation must not leave it closed.
            if (closeRequested && process.HasExited)
            {
                status($"Reopening ProPresenter. Playlist backup (if links changed): {backup}");
                using var restarted = Process.Start(new ProcessStartInfo(executable) { UseShellExecute = true });
                var ready = false;
                for (var attempt = 0; attempt < 60; attempt++)
                {
                    using var probe = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    try { await api.GetVersionAsync(probe.Token); ready = true; break; }
                    catch (Exception e) when (e is System.Net.Http.HttpRequestException or TaskCanceledException) { }
                    await Task.Delay(1000);
                }
                if (!ready) throw new InvalidOperationException($"ProPresenter was reopened but its API is not ready. Check ProPresenter before retrying. Backup: {backup}");
            }
        }
    }

    private static void EnsureClosed()
    {
        var processes = Process.GetProcessesByName("ProPresenter");
        try { if (processes.Length != 0) throw new InvalidOperationException("ProPresenter reopened during sync. No playlist files were replaced."); }
        finally { foreach (var p in processes) p.Dispose(); }
    }
}
