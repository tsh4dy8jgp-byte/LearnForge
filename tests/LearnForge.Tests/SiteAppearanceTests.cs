using System.Net.Http.Json;
using System.Text.Json;
using LearnForge.Api.Contracts.Content;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace LearnForge.Tests;

public class SiteAppearanceTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Public_settings_expose_configured_appearance_as_camel_case_enums()
    {
        using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Site:Layout"] = "Focus", ["Site:ColorScheme"] = "Midnight"
            })));
        using var client = configured.CreateClient();
        var settings = await client.GetFromJsonAsync<JsonElement>("/api/site-settings");
        Assert.Equal("focus", settings.GetProperty("layout").GetString());
        Assert.Equal("midnight", settings.GetProperty("colorScheme").GetString());
    }

    [Fact]
    public void Appearance_defaults_and_all_named_presets_are_valid()
    {
        Assert.True(new SiteSettings().IsValid());
        foreach (var layout in Enum.GetValues<SiteLayout>())
            foreach (var scheme in Enum.GetValues<SiteColorScheme>())
                Assert.True(new SiteSettings { Layout = layout, ColorScheme = scheme }.IsValid());
    }

    [Fact]
    public void Undefined_appearance_values_are_rejected_by_startup_validation()
    {
        Assert.False(new SiteSettings { Layout = (SiteLayout)100 }.IsValid());
        Assert.False(new SiteSettings { ColorScheme = (SiteColorScheme)100 }.IsValid());
    }
}
