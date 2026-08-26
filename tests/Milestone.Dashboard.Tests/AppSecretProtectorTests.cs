using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Milestone.Dashboard.Services;

namespace Milestone.Dashboard.Tests;

public class AppSecretProtectorTests
{
    [Fact]
    public void Protect_then_unprotect_returns_the_original_password()
    {
        var protector = CreateProtector();

        var encrypted = AppSecretProtector.Protect(protector, "FoxSecret!");

        Assert.StartsWith(AppSecretProtector.Prefix, encrypted);
        Assert.NotEqual("FoxSecret!", encrypted);
        Assert.Equal("FoxSecret!", AppSecretProtector.Unprotect(protector, encrypted));
    }

    [Fact]
    public void Unprotect_leaves_plain_text_passwords_unchanged()
    {
        var protector = CreateProtector();

        Assert.Equal("already-plain", AppSecretProtector.Unprotect(protector, "already-plain"));
        Assert.Equal("FoxSecret!", AppSecretProtector.Unprotect(protector, AppSecretProtector.Protect(protector, "FoxSecret!")));
        Assert.Equal("ENC:abc", AppSecretProtector.Protect(protector, "ENC:abc"));
    }

    [Fact]
    public void Writer_replaces_only_the_password_value()
    {
        var folder = Path.Combine(Path.GetTempPath(), "MilestoneDashboardTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "appsettings.json");
        File.WriteAllText(path, """
            {
              "Milestone": {
                "UseDemoData": false,
                "Password": "plain"
              }
            }
            """);

        var writer = new AppSettingsPasswordWriter(path);
        writer.SaveEncrypted("ENC:test-value");

        Assert.False(AppSecretProtector.IsProtected("plain"));
        Assert.True(writer.PasswordIsEncrypted);
        Assert.Equal("ENC:test-value", writer.ReadPassword());
        Assert.Contains("\"UseDemoData\": false", File.ReadAllText(path), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Writer_saves_connection_settings_without_clearing_the_password()
    {
        var folder = Path.Combine(Path.GetTempPath(), "MilestoneDashboardTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "appsettings.json");
        File.WriteAllText(path, """
            {
              "Milestone": {
                "UseDemoData": true,
                "GatewayBaseUrl": "https://xprotect.example.com",
                "Username": "old",
                "Password": "ENC:keep-me",
                "BypassSslValidation": false
              }
            }
            """);

        var writer = new AppSettingsPasswordWriter(path);
        writer.SaveConnection("https://fox-xprotect.local/API", "reader", null, false, true);

        var text = File.ReadAllText(path);
        Assert.Contains("https://fox-xprotect.local", text, StringComparison.Ordinal);
        Assert.DoesNotContain("https://fox-xprotect.local/API", text, StringComparison.Ordinal);
        Assert.Contains("\"Username\": \"reader\"", text, StringComparison.Ordinal);
        Assert.Contains("\"Password\": \"ENC:keep-me\"", text, StringComparison.Ordinal);
        Assert.Contains("\"UseDemoData\": false", text, StringComparison.Ordinal);
        Assert.Contains("\"BypassSslValidation\": true", text, StringComparison.Ordinal);
    }

    private static IDataProtector CreateProtector()
    {
        var services = new ServiceCollection();
        services.AddDataProtection();
        return services.BuildServiceProvider()
            .GetRequiredService<IDataProtectionProvider>()
            .CreateProtector(AppSecretProtector.Purpose);
    }
}
