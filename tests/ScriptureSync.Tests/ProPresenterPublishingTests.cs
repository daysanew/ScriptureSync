using System.Text.Json;
using Google.Protobuf;
using Pro.SerializationInterop.RVProtoData;
using ScriptureSync.Core.Configuration;
using ScriptureSync.Core.Presentations;
using ScriptureSync.ProPresenter;

namespace ScriptureSync.Tests;

public sealed class ProPresenterPublishingTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ScriptureSync-tests-" + Guid.NewGuid());
    private readonly ProPresenterConfiguration _config;
    private readonly FakeApi _api;
    public ProPresenterPublishingTests()
    {
        Directory.CreateDirectory(_root);
        _config = new() { TemplatePath = Path.Combine(_root, "template.pro"), LibraryDirectory = Path.Combine(_root, "library"), LibraryId = Guid.NewGuid().ToString() };
        Directory.CreateDirectory(_config.LibraryDirectory);
        File.WriteAllBytes(_config.TemplatePath, ProPresenterTemplateTests.Fixture().ToByteArray());
        _api = new(_config);
    }
    private ProPresenterPublisher Publisher() => new(_config, Path.Combine(_root, "state"), _api);
    private static ScripturePresentation Content(string text = "Synthetic verse.") => new("Psalm 23:1 (TEST)", [new("Psalm 23:1", text, "TEST")]);

    [Fact]
    public async Task Restart_resolves_changed_library_uuid_by_unique_saved_name_and_folder()
    {
        var newId = Guid.NewGuid().ToString();
        _api.Libraries = [new(newId, "library")];
        var publisher = new ProPresenterPublisher(_config with { LibraryName = "library" }, Path.Combine(_root, "state"), _api);
        await publisher.PublishAsync(Content(), "draft");
        Assert.Equal(newId, _api.LastLibraryId);
    }

    [Fact]
    public async Task Restart_does_not_guess_between_duplicate_library_names()
    {
        _api.Libraries = [new(Guid.NewGuid().ToString(), "library"), new(Guid.NewGuid().ToString(), "library")];
        var publisher = new ProPresenterPublisher(_config with { LibraryName = "library" }, Path.Combine(_root, "state"), _api);
        await Assert.ThrowsAsync<InvalidDataException>(() => publisher.ValidateAsync());
    }

    [Fact]
    public async Task Creates_updates_and_reuses_identity_across_restart_with_backup()
    {
        var first = await Publisher().PublishAsync(Content(), "draft-one");
        Assert.Equal(PublishChange.Create, first.Change);
        var path = Directory.GetFiles(_config.LibraryDirectory).Single();
        var initial = File.ReadAllBytes(path);
        var repeat = await Publisher().PublishAsync(Content(), "draft-one");
        Assert.Equal(PublishChange.Unchanged, repeat.Change);
        Assert.Equal(first.PresentationId, repeat.PresentationId);
        Assert.Equal(initial, File.ReadAllBytes(path));
        var changed = await Publisher().PublishAsync(Content("Changed verse."), "draft-one");
        Assert.Equal(PublishChange.Update, changed.Change);
        Assert.Equal(first.PresentationId, changed.PresentationId);
        Assert.Single(Directory.GetFiles(_config.LibraryDirectory));
        Assert.Equal(initial, File.ReadAllBytes(Directory.GetFiles(Path.Combine(_root, "state", "backups"), "*.pro").Single()));
        var doc = Presentation.Parser.ParseFrom(File.ReadAllBytes(path));
        Assert.Equal(Presentation.Parser.ParseFrom(initial).Arrangements[0].Uuid, doc.Arrangements[0].Uuid);
        Assert.Equal("Psalm", doc.BibleReference.BookName);
        Assert.Equal("PSA", doc.BibleReference.BookKey);
        Assert.Equal(PublishChange.Unchanged, Publisher().Preview(Content("Changed verse."), "draft-one").Change);
    }
    [Fact]
    public async Task Outside_edits_and_preview_drift_are_never_overwritten()
    {
        var publisher = Publisher();
        await publisher.PublishAsync(Content(), "draft");
        var plan = publisher.Preview(Content("New."), "draft");
        var doc = Presentation.Parser.ParseFrom(File.ReadAllBytes(plan.Path));
        doc.Name = "Human edit";
        var edited = doc.ToByteArray();
        File.WriteAllBytes(plan.Path, edited);
        await Assert.ThrowsAsync<InvalidOperationException>(() => publisher.ApplyAsync(plan));
        Assert.Throws<InvalidDataException>(() => publisher.Preview(Content(), "draft"));
        Assert.Equal(edited, File.ReadAllBytes(plan.Path));
    }
    [Fact]
    public async Task Cancellation_and_connection_failure_do_not_publish()
    {
        var publisher = Publisher();
        var plan = publisher.Preview(Content(), "draft");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => publisher.ApplyAsync(plan, new CancellationToken(true)));
        _api.Offline = true;
        await Assert.ThrowsAsync<HttpRequestException>(() => publisher.ApplyAsync(plan));
        Assert.Empty(Directory.GetFiles(_config.LibraryDirectory));
    }
    [Fact]
    public async Task Stale_presentations_are_reported_and_retained()
    {
        var publisher = Publisher();
        await publisher.PublishAsync(Content(), "draft");
        Assert.Single(publisher.FindStale([]));
        Assert.Single(Directory.GetFiles(_config.LibraryDirectory));
    }
    [Fact]
    public void Does_not_overwrite_existing_unowned_path()
    {
        var publisher = Publisher();
        var plan = publisher.Preview(Content(), "draft");
        File.WriteAllBytes(plan.Path, ProPresenterTemplateTests.Fixture().ToByteArray());
        Assert.Throws<InvalidDataException>(() => publisher.Preview(Content(), "draft"));
    }
    public void Dispose() => Directory.Delete(_root, true);
    private sealed class FakeApi(ProPresenterConfiguration config) : IProPresenterApiClient
    {
        public bool Offline { get; set; }
        public IReadOnlyList<ProPresenterItem> Libraries { get; set; } = [new(config.LibraryId, "Test")];
        public string? LastLibraryId { get; private set; }
        public Task<ProPresenterVersion> GetVersionAsync(CancellationToken token = default) => Offline ? throw new HttpRequestException("Offline") : Task.FromResult(new ProPresenterVersion("Test", "v1", "windows"));
        public Task<IReadOnlyList<ProPresenterItem>> GetLibrariesAsync(CancellationToken token = default) => Task.FromResult(Libraries);
        public Task<IReadOnlyList<ProPresenterItem>> GetPresentationsAsync(string id, CancellationToken token = default)
        {
            LastLibraryId = id;
            return Task.FromResult<IReadOnlyList<ProPresenterItem>>(Directory.GetFiles(config.LibraryDirectory, "*.pro").Select(p => Presentation.Parser.ParseFrom(File.ReadAllBytes(p))).Select(p => new ProPresenterItem(p.Uuid.String, p.Name)).ToArray());
        }
        public Task<IReadOnlyList<string>> GetPresentationSlideTextsAsync(string id, CancellationToken token = default)
        {
            var doc = Directory.GetFiles(config.LibraryDirectory, "*.pro").Select(p => Presentation.Parser.ParseFrom(File.ReadAllBytes(p))).Single(p => p.Uuid.String == id);
            return Task.FromResult<IReadOnlyList<string>>(doc.Cues.Select(c => string.Join("\n", c.Actions[0].Slide.Presentation.BaseSlide.Elements.Select(e => e.Element_.Text.RtfData.ToStringUtf8().Split("\\cb2 ")[1].TrimEnd('}')))).ToArray());
        }
        public Task<IReadOnlyList<ProPresenterItem>> GetPlaylistsAsync(CancellationToken token = default) => throw new NotSupportedException();
        public Task<JsonElement> GetPlaylistContentsAsync(string id, CancellationToken token = default) => throw new NotSupportedException();
        public Task UpdatePlaylistAsync(string id, JsonElement expected, JsonElement items, CancellationToken token = default) => throw new NotSupportedException();
    }
}

public sealed class ProPresenterPlaylistTests
{
    private static JsonElement Snapshot(string items) => JsonDocument.Parse("{\"items\":" + items + "}").RootElement.Clone();
    [Fact]
    public void Appending_preserves_order_and_unrelated_items()
    {
        var snapshot = Snapshot("""[{"id":{"uuid":"song","name":"Song","index":0},"type":"presentation","target_uuid":"song-target"}]""");
        var output = ProPresenterPlaylistLinker.BuildItems(snapshot, [new("generated", "Psalm", null)]);
        for (var i = 0; i < 1; i++) Assert.True(JsonElement.DeepEquals(snapshot.GetProperty("items")[i], output[i]));
        Assert.Equal("generated", output[1].GetProperty("id").GetProperty("uuid").GetString());
        var repeat = ProPresenterPlaylistLinker.BuildItems(Snapshot(output.GetRawText()), [new("generated", "Psalm", null)]);
        Assert.True(JsonElement.DeepEquals(output, repeat));
    }
    [Fact]
    public void Pco_playlists_are_blocked_until_link_identity_is_verified()
    {
        var snapshot = Snapshot("""[{"type":"placeholder","is_pco":true}]""");
        Assert.Throws<InvalidOperationException>(() => ProPresenterPlaylistLinker.BuildItems(snapshot, [new("generated", "Psalm", null)]));
    }
    [Fact]
    public void Repeat_keeps_existing_arrangement_and_does_not_duplicate()
    {
        var snapshot = Snapshot("""[{"id":{"uuid":"item","name":"Title"},"type":"presentation","target_uuid":"generated","presentation_info":{"presentation_uuid":"generated","arrangement_name":"Custom"}}]""");
        var output = ProPresenterPlaylistLinker.BuildItems(snapshot, [new("generated", "Title", null)]);
        Assert.True(JsonElement.DeepEquals(snapshot.GetProperty("items"), output));
    }
    [Fact]
    public void Conflicting_targets_and_song_replacement_are_rejected()
    {
        var snapshot = Snapshot("""[{"type":"placeholder"},{"type":"presentation","target_uuid":"song"}]""");
        Assert.Throws<InvalidOperationException>(() => ProPresenterPlaylistLinker.BuildItems(snapshot, [new("a", "A", 0), new("b", "B", 0)]));
        Assert.Throws<InvalidOperationException>(() => ProPresenterPlaylistLinker.BuildItems(snapshot, [new("a", "A", 1)]));
    }
    [Fact]
    public void Existing_link_cannot_be_duplicated_into_another_placeholder()
    {
        var snapshot = Snapshot("""[{"type":"placeholder"},{"type":"presentation","target_uuid":"a"}]""");
        Assert.Throws<InvalidOperationException>(() => ProPresenterPlaylistLinker.BuildItems(snapshot, [new("a", "A", 0)]));
    }
}
