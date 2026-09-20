using ScriptureSync.App.Services;
using ScriptureSync.App.ViewModels;
using ScriptureSync.Core.Configuration;
using ScriptureSync.Core.Logging;
using ScriptureSync.Core.Parsing;
using ScriptureSync.OpenLP;
using ScriptureSync.PlanningCenter;

namespace ScriptureSync.Tests;

public sealed class ManualDraftWorkflowTests : IDisposable
{
    [Fact]
    public void ProPresenter_reimport_preserves_identity_removes_stale_rows_and_uses_plan_order()
    {
        var vm = CreateViewModel();
        vm.AddPlanningCenterItems([new("a", 1, "Scripture", "John 3:16\nJohn 3:17"), new("b", 2, "Scripture", "Psalm 23")], "Plan", "type:plan", true);
        var id = vm.Items[0].Id;
        vm = CreateViewModel();
        vm.AddPlanningCenterItems([new("b", 1, "Scripture", "Psalm 23"), new("a", 2, "Scripture", "John 3:18")], "Plan", "type:plan", true);
        Assert.Equal(2, vm.Items.Count);
        Assert.Equal("Psalm 23", vm.Items[0].RawText);
        Assert.Equal(id, vm.Items[1].Id);
        Assert.Equal("John 3:18", CreateViewModel().Items[1].RawText);
        Assert.Equal("PCO:type:plan:a:0", vm.Items[1].SourceKey);
    }
    private readonly string _temporaryRoot = Path.Combine(
        Path.GetTempPath(),
        $"ScriptureSyncTests-{Guid.NewGuid():N}");

    [Fact]
    public void Multiline_paste_creates_editable_rows_with_live_validation()
    {
        var viewModel = CreateViewModel();

        viewModel.AddPastedText("John 3:16 (KJV)\r\nRomans 8:28 KJV\r\nBad input");

        Assert.Equal(3, viewModel.Items.Count);
        Assert.Equal(2, viewModel.ReadyCount);
        Assert.Equal(1, viewModel.AttentionCount);
        Assert.Equal("John 3:16 (KJV)", viewModel.Items[0].NormalizedText);

        viewModel.Items[2].RawText = "Psalm 23 (KJV)";

        Assert.Equal(3, viewModel.ReadyCount);
        Assert.Equal("Psalm 23 (KJV)", viewModel.Items[2].NormalizedText);
    }

    [Fact]
    public void Manual_draft_is_reloaded_after_the_application_restarts()
    {
        var firstViewModel = CreateViewModel();
        firstViewModel.AddPastedText("John 3:16 (KJV)\nRomans 8:28 (KJV)");

        var reloadedViewModel = CreateViewModel();

        Assert.Equal(2, reloadedViewModel.Items.Count);
        Assert.Equal("John 3:16 (KJV)", reloadedViewModel.Items[0].RawText);
        Assert.Equal("Romans 8:28 (KJV)", reloadedViewModel.Items[1].RawText);
    }

    [Fact]
    public void Multiple_translations_are_shown_in_the_editable_preview()
    {
        var viewModel = CreateViewModel();

        viewModel.AddPastedText("1 Peter 1:3 (NKJV & NLT & KJV)");

        Assert.Equal(1, viewModel.ReadyCount);
        Assert.Equal("1 Peter 1:3 (NKJV & NLT & KJV)", viewModel.Items[0].NormalizedText);
    }

    [Fact]
    public void Wednesday_scripture_list_loads_as_twenty_three_ready_rows()
    {
        var viewModel = CreateViewModel();
        const string scriptureList = """
            1 Corinthians 13:13 (NKJV)

            Psalm 71:5 (NLT)

            Joshua 2:18 & 21 (KJV)

            Isaiah 40:31 (KJV) 

            Lamentations 3:25-26 (KJV)

            Psalm 31:24 (NKJV)

            Psalm 42:11 (NLT)

            Psalm 34:8 (NKJV)

            Psalm 57:1 (NKJV)

            Jeremiah 17:5 (NKJV)

            Jeremiah 17:7 (NKJV)

            1 Peter 1:3 (NKJV & NLT)

            1 Corinthians 5:19-23 (AMP)

            Galatians 5:5 (AMP)

            Romans 5:1-2 (AMP)

            Romans 5:5 (AMP)

            Romans 12:12 (AMP)

            Psalm 146:5 (NLT)

            1 John 3:3 (AMP)

            1 Peter 3:15 (NLT)

            Romans 15:13 (NLT)

            Hebrews 6:19 (AMP)

            Romans 5:5 (AMP)


            """;

        viewModel.AddPastedText(scriptureList);

        Assert.Equal(23, viewModel.Items.Count);
        Assert.Equal(23, viewModel.ReadyCount);
        Assert.Equal(0, viewModel.AttentionCount);
        Assert.Equal(
            "1 Peter 1:3 (NKJV & NLT)",
            viewModel.Items.Single(item => item.RawText.StartsWith("1 Peter 1:3")).NormalizedText);
        Assert.Equal(2, viewModel.Items.Count(item => item.NormalizedText == "Romans 5:5 (AMP)"));
    }

    [Fact]
    public async Task Sync_adds_each_translation_and_preserves_duplicate_rows()
    {
        var client = new FakeOpenLpClient();
        var viewModel = CreateViewModel(client);
        viewModel.AddPastedText("1 Peter 1:3 (NKJV & NLT)\nRomans 5:5 (AMP)\nRomans 5:5 (AMP)");

        await viewModel.SyncToOpenLpAsync();

        Assert.Equal(
            [
                ("NKJV", "1 Peter 1:3"),
                ("NLT", "1 Peter 1:3"),
                ("AMP", "Romans 5:5"),
                ("AMP", "Romans 5:5")
            ],
            client.AddAttempts);
        Assert.All(viewModel.Items, item => Assert.StartsWith("Added", item.Status));
    }

    [Fact]
    public async Task Sync_reports_a_scripture_that_OpenLP_cannot_find()
    {
        var client = new FakeOpenLpClient { MissingReference = "1 Corinthians 5:19-23" };
        var viewModel = CreateViewModel(client);
        viewModel.AddPastedText("1 Corinthians 5:19-23 (AMP)");

        await viewModel.SyncToOpenLpAsync();

        Assert.Equal("Not found: 1 Corinthians 5:19-23 (AMP)", viewModel.Items[0].Status);
        Assert.Single(client.AddAttempts);
        Assert.Contains("1 need attention", viewModel.OpenLpStatus);
    }

    [Fact]
    public async Task Sync_stops_immediately_when_OpenLP_disconnects()
    {
        var client = new FakeOpenLpClient { DisconnectOnReference = "Psalm 71:5" };
        var viewModel = CreateViewModel(client);
        viewModel.AddPastedText(
            "Romans 5:5 (KJV)\nPsalm 71:5 (NLT)\nIsaiah 40:31 (KJV)");

        await viewModel.SyncToOpenLpAsync();

        Assert.Equal(
            [("KJV", "Romans 5:5"), ("NLT", "Psalm 71:5")],
            client.AddAttempts);
        Assert.DoesNotContain(client.AddAttempts,
            attempt => attempt.Reference == "Isaiah 40:31");
        Assert.Contains("plugin unavailable after 1 added", viewModel.OpenLpStatus);
        Assert.Equal("Stopped: ScriptureSync plugin unavailable", viewModel.Items[1].Status);
        Assert.Equal("Ready", viewModel.Items[2].Status);
    }

    [Fact]
    public async Task Plugin_status_check_reports_ready_with_installed_Bible_count()
    {
        var client = new FakeOpenLpClient();
        var viewModel = CreateViewModel(client);

        var available = await viewModel.CheckOpenLpPluginAsync();

        Assert.True(available);
        Assert.Equal("Ready • plugin active • 3 Bibles", viewModel.OpenLpStatus);
    }

    [Fact]
    public async Task Sync_stops_before_adding_when_plugin_status_check_fails()
    {
        var client = new FakeOpenLpClient
        {
            PrepareException = new HttpRequestException("Connection refused.")
        };
        var viewModel = CreateViewModel(client);
        viewModel.AddPastedText("John 3:16 (KJV)");

        await viewModel.SyncToOpenLpAsync();

        Assert.Empty(client.AddAttempts);
        Assert.Equal("Plugin unavailable • start OpenLP and activate ScriptureSync", viewModel.OpenLpStatus);
        Assert.Equal("Ready", viewModel.Items[0].Status);
    }

    [Fact]
    public void Clear_all_removes_every_row_and_clears_the_saved_draft()
    {
        var viewModel = CreateViewModel();
        viewModel.AddPastedText("John 3:16 (KJV)\nPsalm 23:1 (NLT)");

        viewModel.ClearAll();

        Assert.Empty(viewModel.Items);
        Assert.Equal("Add or paste scripture references to begin.", viewModel.SummaryText);
        Assert.Empty(CreateViewModel().Items);
    }

    [Fact]
    public void Planning_Center_import_adds_each_detail_line_in_item_order()
    {
        var viewModel = CreateViewModel();
        var count = viewModel.AddPlanningCenterItems(
        [
            new PlanningCenterScriptureItem("2", 2, "Message", "Romans 5:5 (NLT)"),
            new PlanningCenterScriptureItem("1", 1, "Scripture", "John 3:16 (KJV)\nPsalm 23:1 (NLT)")
        ], "Sunday — Aug 30");

        Assert.Equal(3, count);
        Assert.Equal(["John 3:16 (KJV)", "Psalm 23:1 (NLT)", "Romans 5:5 (NLT)"],
            viewModel.Items.Select(item => item.RawText));
        Assert.All(viewModel.Items, item => Assert.StartsWith("Planning Center", item.Source));
    }

    [Fact]
    public async Task Missing_translation_skips_its_remaining_passages_but_continues_other_translations()
    {
        var client = new FakeOpenLpClient { MissingTranslation = "KJV" };
        var viewModel = CreateViewModel(client);
        viewModel.AddPastedText("John 3:16; Romans 8:28 (KJV & NLT)\nPsalm 23 (NLT)");

        await viewModel.SyncToOpenLpAsync();

        Assert.Equal(
            [("KJV", "John 3:16"), ("NLT", "John 3:16"), ("NLT", "Romans 8:28"), ("NLT", "Psalm 23")],
            client.AddAttempts);
        Assert.Equal("Added 2; Translation not installed: KJV", viewModel.Items[0].Status);
        Assert.Equal("Added 1 to OpenLP", viewModel.Items[1].Status);
        Assert.Equal("Sync complete • 3 added • 1 need attention", viewModel.OpenLpStatus);
        Assert.False(viewModel.IsSyncing);
    }

    [Fact]
    public async Task Plugin_failure_during_prepare_preserves_error_and_does_not_add()
    {
        var client = new FakeOpenLpClient { PrepareException = new OpenLpException("Not ready") };
        var viewModel = CreateViewModel(client);
        viewModel.AddPastedText("John 3:16 (KJV)");

        await viewModel.SyncToOpenLpAsync();

        Assert.Empty(client.AddAttempts);
        Assert.Equal("Plugin error • Not ready", viewModel.OpenLpStatus);
        Assert.False(viewModel.IsSyncing);
        Assert.True(viewModel.SyncCommand.CanExecute(null));
    }

    [Fact]
    public async Task Plugin_add_failure_continues_later_rows_without_counting_failed_addition()
    {
        var client = new FakeOpenLpClient { FailOnReference = "John 3:16" };
        var viewModel = CreateViewModel(client);
        viewModel.AddPastedText("John 3:16 (KJV)\nPsalm 23 (KJV)");

        await viewModel.SyncToOpenLpAsync();

        Assert.Equal("KJV: Add rejected", viewModel.Items[0].Status);
        Assert.Equal("Added 1 to OpenLP", viewModel.Items[1].Status);
        Assert.Equal("Sync complete • 1 added • 1 need attention", viewModel.OpenLpStatus);
        Assert.Equal(2, client.AddAttempts.Count);
    }

    public void Dispose()
    {
        if (Directory.Exists(_temporaryRoot))
        {
            Directory.Delete(_temporaryRoot, recursive: true);
        }
    }

    private MainWindowViewModel CreateViewModel(IOpenLpClient? openLpClient = null)
    {
        var paths = new LocalAppPaths(_temporaryRoot);
        var logger = new SilentLogger();
        return new MainWindowViewModel(
            new ScriptureReferenceParser(),
            new ManualDraftStore(paths, logger),
            openLpClient is null ? null : new OpenLpSyncDestination(openLpClient),
            logger);
    }

    private sealed class FakeOpenLpClient : IOpenLpClient
    {
        public string? MissingReference { get; init; }
        public string? DisconnectOnReference { get; init; }
        public string? MissingTranslation { get; init; }
        public string? FailOnReference { get; init; }
        public Exception? PrepareException { get; init; }
        public List<(string Translation, string Reference)> AddAttempts { get; } = [];

        public Task<OpenLpConnectionInfo> GetConnectionInfoAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new OpenLpConnectionInfo(2, 6,
                new Dictionary<string, string>
                {
                    ["KJV"] = "King James Version",
                    ["NLT"] = "New Living Translation",
                    ["AMP"] = "Amplified Bible"
                }, "KJV"));

        public Task<OpenLpConnectionInfo> PrepareAsync(CancellationToken cancellationToken = default)
        {
            if (PrepareException is not null)
            {
                throw PrepareException;
            }

            return GetConnectionInfoAsync(cancellationToken);
        }

        public Task<OpenLpAddResult?> AddScriptureAsync(
            string translationCode,
            string reference,
            CancellationToken cancellationToken = default)
        {
            AddAttempts.Add((translationCode, reference));
            if (translationCode == MissingTranslation)
                throw new OpenLpBibleNotInstalledException(translationCode);
            if (reference == FailOnReference)
                throw new OpenLpException("Add rejected");
            if (reference == DisconnectOnReference)
            {
                throw new HttpRequestException("OpenLP exited.");
            }
            return Task.FromResult(reference == MissingReference
                ? null
                : new OpenLpAddResult(reference, translationCode, $"{reference} ({translationCode})"));
        }
    }

    private sealed class SilentLogger : IAppLogger
    {
        public void Info(string message)
        {
        }

        public void Error(string message, Exception? exception = null)
        {
        }
    }
}
