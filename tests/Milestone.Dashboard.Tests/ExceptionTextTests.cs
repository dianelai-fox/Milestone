using Milestone.Dashboard.Services;

namespace Milestone.Dashboard.Tests;

public class ExceptionTextTests
{
    [Fact]
    public void Flatten_includes_the_inner_ssl_reason()
    {
        var inner = new System.Security.Authentication.AuthenticationException(
            "The remote certificate is invalid because of errors in the certificate chain: UntrustedRoot");
        var error = new HttpRequestException("The SSL connection could not be established, see inner exception.", inner);

        var text = ExceptionText.Flatten(error);

        Assert.Contains("SSL connection could not be established", text);
        Assert.Contains("UntrustedRoot", text);
    }

    [Fact]
    public void Flatten_skips_duplicate_inner_messages()
    {
        var inner = new InvalidOperationException("same");
        var error = new InvalidOperationException("same", inner);

        Assert.Equal("same", ExceptionText.Flatten(error));
    }
}
