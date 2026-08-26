using System.Text.Json;
using Milestone.Dashboard.Options;

namespace Milestone.Dashboard.Services;

public static class XprotectAuth
{
    public static string NormalizeGatewayBaseUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        var trimmed = url.Trim().TrimEnd('/');
        if (trimmed.EndsWith("/API", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[..^4].TrimEnd('/');
        }

        return trimmed;
    }

    public static IReadOnlyList<string> TokenUrlCandidates(
        string? gatewayBaseUrl,
        string? tokenUrl,
        string? identityProvider = null)
    {
        if (!string.IsNullOrWhiteSpace(tokenUrl))
        {
            return [tokenUrl.Trim()];
        }

        var root = NormalizeGatewayBaseUrl(gatewayBaseUrl);
        var urls = new List<string>();
        if (!string.IsNullOrWhiteSpace(identityProvider))
        {
            urls.Add($"{identityProvider.Trim().TrimEnd('/')}/connect/token");
        }

        if (!string.IsNullOrWhiteSpace(root))
        {
            urls.Add($"{root}/API/IDP/connect/token");
            urls.Add($"{root}/IDP/connect/token");
        }

        return urls
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string? Validate(MilestoneOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Username))
        {
            return "Milestone:Username is empty. Set an XProtect Basic user from Management Client.";
        }

        if (string.IsNullOrWhiteSpace(options.Password))
        {
            return "Milestone:Password is empty. Set the Basic user password, or encrypt it on this IIS server with scripts/encrypt-password.ps1.";
        }

        var host = NormalizeGatewayBaseUrl(options.GatewayBaseUrl);
        if (string.IsNullOrWhiteSpace(host) || host.Contains("example.com", StringComparison.OrdinalIgnoreCase))
        {
            return "Milestone:GatewayBaseUrl is still the example host. Set it to your management server root, for example https://foxuswdmsia297, with no /API suffix.";
        }

        return null;
    }

    public static bool ShouldTryNextTokenUrl(int statusCode, string? errorCode)
    {
        if (statusCode is 404 or 405)
        {
            return true;
        }

        return statusCode == 400 && errorCode is null or "invalid_request";
    }

    public static string? ReadErrorCode(string? body) => ReadJsonString(body, "error");

    public static string? ReadErrorDescription(string? body) => ReadJsonString(body, "error_description");

    public static string Explain(int statusCode, string tokenUrl, string? body)
    {
        var code = ReadErrorCode(body);
        var description = ReadErrorDescription(body);
        var hint = code switch
        {
            "invalid_grant" =>
                "XProtect rejected this user. Use a Basic user created in Management Client, not a Windows login, and confirm the password.",
            "invalid_client" =>
                "XProtect rejected client_id. Leave ClientId as GrantValidatorClient unless your IDP uses another client.",
            "invalid_request" =>
                "The token URL or request was rejected. Set GatewayBaseUrl to the management server root (no /API), or set TokenUrl to IdentityProvider from /api/.well-known/uris plus /connect/token.",
            _ => "Check Username, Password, GatewayBaseUrl, and that the account is an XProtect Basic user."
        };

        var detail = string.IsNullOrWhiteSpace(code) ? string.Empty : $": {code}";
        if (!string.IsNullOrWhiteSpace(description))
        {
            detail += $" ({description})";
        }

        return $"XProtect login failed (HTTP {statusCode}) at {tokenUrl}{detail}. {hint}";
    }

    private static string? ReadJsonString(string? body, string name)
    {
        if (string.IsNullOrWhiteSpace(body) || body[0] is not '{' and not '[')
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.TryGetProperty(name, out var value)
                ? value.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
