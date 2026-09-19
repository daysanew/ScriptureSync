using ScriptureSync.Core.Parsing;
using ScriptureSync.Core.Sync;
using ScriptureSync.OpenLP;

namespace ScriptureSync.Tests;

public sealed class OpenLpSyncDestinationTests
{
    [Theory]
    [InlineData("John 3:16-4:2")]
    [InlineData("Psalm 23")]
    public async Task Adapter_preserves_reference_translation_confirmation_and_token(string reference)
    {
        var client = new StubClient();
        var destination = new OpenLpSyncDestination(client);
        using var cancellation = new CancellationTokenSource();
        var passage = Assert.Single(new ScriptureReferenceParser().Parse(reference).Passages);

        var status = await destination.PrepareAsync(cancellation.Token);
        Assert.Equal(cancellation.Token, client.Token);
        var result = await destination.AddScriptureAsync("KJV", passage, cancellation.Token);

        Assert.Equal(1, status.InstalledBibleCount);
        Assert.Equal(("KJV", reference), client.Request);
        Assert.Equal(cancellation.Token, client.Token);
        Assert.Equal(new ScriptureSyncResult(reference, "Installed Bible", "Confirmed title"), result);
        Assert.Equal(1, client.AddCount);
    }

    [Fact]
    public async Task No_verses_remains_an_unconfirmed_addition()
    {
        var destination = new OpenLpSyncDestination(new StubClient { NotFound = true });
        var passage = Assert.Single(new ScriptureReferenceParser().Parse("John 3:16").Passages);
        Assert.Null(await destination.AddScriptureAsync("KJV", passage));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Connection_and_plugin_errors_are_neutral_and_keep_diagnostics(bool duringPrepare)
    {
        var passage = Assert.Single(new ScriptureReferenceParser().Parse("John 3:16").Passages);
        foreach (var original in new Exception[]
        {
            new HttpRequestException("Connection refused"),
            new OpenLpException("Bridge rejected request")
        })
        {
            var destination = new OpenLpSyncDestination(new StubClient { Failure = original });
            async Task Act()
            {
                if (duringPrepare) await destination.PrepareAsync();
                else await destination.AddScriptureAsync("KJV", passage);
            }

            var error = await Assert.ThrowsAnyAsync<ScriptureSyncException>(Act);
            Assert.Equal(original.Message, error.Message);
            Assert.Same(original, error.InnerException);
            Assert.Equal(original is HttpRequestException, error is ScriptureDestinationUnavailableException);
        }
    }

    [Fact]
    public async Task Missing_translation_has_a_neutral_typed_error()
    {
        var original = new OpenLpBibleNotInstalledException("NLT");
        var destination = new OpenLpSyncDestination(new StubClient { Failure = original });
        var passage = Assert.Single(new ScriptureReferenceParser().Parse("John 3:16").Passages);

        var error = await Assert.ThrowsAsync<ScriptureTranslationNotInstalledException>(
            () => destination.AddScriptureAsync("NLT", passage));

        Assert.Equal("NLT", error.TranslationCode);
        Assert.Same(original, error.InnerException);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancellation_is_not_reclassified_as_a_destination_error(bool duringPrepare)
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var destination = new OpenLpSyncDestination(new StubClient());
        var passage = Assert.Single(new ScriptureReferenceParser().Parse("John 3:16").Passages);

        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            if (duringPrepare) await destination.PrepareAsync(cancellation.Token);
            else await destination.AddScriptureAsync("KJV", passage, cancellation.Token);
        });
        Assert.Equal(cancellation.Token, error.CancellationToken);
    }

    private sealed class StubClient : IOpenLpClient
    {
        public Exception? Failure { get; init; }
        public bool NotFound { get; init; }
        public CancellationToken Token { get; private set; }
        public (string Translation, string Reference) Request { get; private set; }
        public int AddCount { get; private set; }

        public Task<OpenLpConnectionInfo> GetConnectionInfoAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The adapter must use PrepareAsync.");

        public Task<OpenLpConnectionInfo> PrepareAsync(CancellationToken cancellationToken = default)
        {
            Token = cancellationToken;
            cancellationToken.ThrowIfCancellationRequested();
            if (Failure is not null) throw Failure;
            return Task.FromResult(new OpenLpConnectionInfo(1, 0,
                new Dictionary<string, string> { ["KJV"] = "Installed Bible" }, "KJV"));
        }

        public Task<OpenLpAddResult?> AddScriptureAsync(string translationCode, string reference,
            CancellationToken cancellationToken = default)
        {
            Token = cancellationToken;
            Request = (translationCode, reference);
            AddCount++;
            cancellationToken.ThrowIfCancellationRequested();
            if (Failure is not null) throw Failure;
            return Task.FromResult(NotFound ? null :
                new OpenLpAddResult(reference, "Installed Bible", "Confirmed title"));
        }
    }
}
