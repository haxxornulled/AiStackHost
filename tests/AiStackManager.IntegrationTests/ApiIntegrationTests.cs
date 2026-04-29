using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AiStackManager.IntegrationTests;

public sealed class ApiIntegrationTests
{
    [Fact]
    public async Task HealthEndpoint_ReturnsSuccess()
    {
        await using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();

        var res = await client.GetAsync("/health");

        Assert.True(res.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Models_Providers_ReturnsArray_WhenAuthorized()
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, cfg) =>
            {
                var dict = new Dictionary<string, string>
                {
                    ["AiStack:ManagementToken"] = "test-token",
                    ["AiStack:AllowManagementWithoutTokenFromLoopback"] = "false"
                };
                cfg.AddInMemoryCollection(dict);
            });
        });

        await using var _ = factory;
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-AiStack-Token", "test-token");

        var res = await client.GetAsync("/api/models/providers");
        res.EnsureSuccessStatusCode();

        var json = await res.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
    }
}
