using System.Net;
using System.Text;
using ScriptureSync.Core.Models;
using ScriptureSync.PlanningCenter;

namespace ScriptureSync.Tests;

public sealed class PlanningCenterClientTests
{
    [Fact]
    public async Task Upcoming_plans_are_filtered_to_the_configured_window_and_sorted()
    {
        var handler = new StubHandler(request => request.RequestUri!.AbsolutePath switch
        {
            "/services/v2/service_types" => Json("""
                {"data":[{"id":"type-1","attributes":{"name":"Sunday"}}]}
                """),
            _ => Json("""
                {"data":[
                  {"id":"late","attributes":{"title":"Later","sort_date":"2026-08-27T10:00:00-05:00"}},
                  {"id":"soon","attributes":{"title":"Soon","sort_date":"2026-08-25T10:00:00-05:00"}},
                  {"id":"far","attributes":{"title":"Far","sort_date":"2026-09-10T10:00:00-05:00"}}
                ]}
                """)
        });
        var client = CreateClient(handler);

        var plans = await client.GetUpcomingPlansAsync(7);

        Assert.Equal(["soon", "late"], plans.Select(plan => plan.Id));
        Assert.All(plans, plan => Assert.Equal("Sunday", plan.ServiceTypeName));
        Assert.StartsWith("Basic ", handler.LastAuthorization);
    }

    [Fact]
    public async Task Scripture_items_match_multiple_configured_names_and_use_details_in_sequence_order()
    {
        var handler = new StubHandler(_ => Json("""
            {"data":[
              {"id":"3","attributes":{"title":"Notices","sequence":3,"description":"Ignore me","html_details":""}},
              {"id":"2","attributes":{"title":"Message Text","sequence":2,"description":"","html_details":"<p>Romans 5:5 (NLT)</p>"}},
              {"id":"1","attributes":{"title":"SCRIPTURE","sequence":1,"description":"","html_details":"<div>John 3:16 (KJV)<br>Psalm 23:1 (NLT)</div>"}}
            ]}
            """));
        var client = CreateClient(handler);
        var plan = new ServicePlan("plan-1", "type-1", "Sunday", "Morning",
            new DateTimeOffset(2026, 8, 25, 10, 0, 0, TimeSpan.FromHours(-5)));

        var items = await client.GetScriptureItemsAsync(plan, ["Scripture", "Message Text"]);

        Assert.Equal(["1", "2"], items.Select(item => item.Id));
        Assert.Equal("John 3:16 (KJV)\nPsalm 23:1 (NLT)", items[0].Details);
        Assert.Equal("Romans 5:5 (NLT)", items[1].Details);
    }

    private static PlanningCenterClient CreateClient(HttpMessageHandler handler) => new(
        "application-id", "secret",
        new HttpClient(handler) { BaseAddress = new Uri("https://api.planningcenteronline.com/services/v2/") },
        new FixedTimeProvider(new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.FromHours(-5))));

    private static HttpResponseMessage Json(string value) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(value, Encoding.UTF8, "application/json")
    };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        public string? LastAuthorization { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastAuthorization = request.Headers.Authorization?.ToString();
            return Task.FromResult(response(request));
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.CreateCustomTimeZone(
            "Test", now.Offset, "Test", "Test");
    }
}
