using Milestone.Dashboard.Options;
using Milestone.Dashboard.Services;

namespace Milestone.Dashboard.Tests;

public class XprotectAuthTests
{
    [Theory]
    [InlineData("https://fox-xprotect.local/", "https://fox-xprotect.local")]
    [InlineData("https://fox-xprotect.local/API", "https://fox-xprotect.local")]
    [InlineData("https://fox-xprotect.local/api/", "https://fox-xprotect.local")]
    public void Normalizes_gateway_root(string input, string expected)
    {
        Assert.Equal(expected, XprotectAuth.NormalizeGatewayBaseUrl(input));
    }

    [Fact]
    public void Token_candidates_include_api_and_idp_paths()
    {
        var urls = XprotectAuth.TokenUrlCandidates("https://fox-xprotect.local/API", null, "https://fox-xprotect.local/IDP");

        Assert.Equal("https://fox-xprotect.local/IDP/connect/token", urls[0]);
        Assert.Contains("https://fox-xprotect.local/API/IDP/connect/token", urls);
        Assert.Contains("https://fox-xprotect.local/IDP/connect/token", urls);
    }

    [Fact]
    public void Explains_invalid_grant_as_basic_user_problem()
    {
        var message = XprotectAuth.Explain(400, "https://host/API/IDP/connect/token", """{"error":"invalid_grant"}""");

        Assert.Contains("HTTP 400", message);
        Assert.Contains("invalid_grant", message);
        Assert.Contains("Basic user", message);
    }

    [Fact]
    public void Validates_missing_live_settings()
    {
        Assert.Contains("Username", XprotectAuth.Validate(new MilestoneOptions()));
        Assert.Contains("example host", XprotectAuth.Validate(new MilestoneOptions
        {
            Username = "reader",
            Password = "secret",
            GatewayBaseUrl = "https://xprotect.example.com"
        }));
        Assert.Null(XprotectAuth.Validate(new MilestoneOptions
        {
            Username = "reader",
            Password = "secret",
            GatewayBaseUrl = "https://fox-xprotect.local/API"
        }));
    }

    [Fact]
    public void Tries_next_url_for_invalid_request_but_not_invalid_grant()
    {
        Assert.True(XprotectAuth.ShouldTryNextTokenUrl(400, "invalid_request"));
        Assert.True(XprotectAuth.ShouldTryNextTokenUrl(404, null));
        Assert.False(XprotectAuth.ShouldTryNextTokenUrl(400, "invalid_grant"));
    }
}
