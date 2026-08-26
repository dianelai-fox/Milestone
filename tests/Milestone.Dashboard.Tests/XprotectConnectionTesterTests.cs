using Milestone.Dashboard.Services;

namespace Milestone.Dashboard.Tests;

public class XprotectConnectionTesterTests
{
    [Fact]
    public async Task Test_returns_validation_error_without_calling_xprotect()
    {
        var tester = new XprotectConnectionTester();

        var error = await tester.TestAsync(
            "https://xprotect.example.com",
            "reader",
            "secret",
            "GrantValidatorClient",
            bypassSsl: true,
            CancellationToken.None);

        Assert.Contains("example host", error);
    }

    [Fact]
    public async Task Test_requires_a_basic_user()
    {
        var tester = new XprotectConnectionTester();

        var error = await tester.TestAsync(
            "https://fox-xprotect.local",
            "",
            "secret",
            "GrantValidatorClient",
            bypassSsl: false,
            CancellationToken.None);

        Assert.Contains("Username", error);
    }
}
