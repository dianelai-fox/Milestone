using System.Text.Json;
using Milestone.Dashboard.Options;

namespace Milestone.Dashboard.Services;

public sealed class XprotectConnectionTester
{
    public async Task<string?> TestAsync(
        string gatewayBaseUrl,
        string username,
        string password,
        string? clientId,
        bool bypassSsl,
        CancellationToken cancellationToken)
    {
        var options = new MilestoneOptions
        {
            GatewayBaseUrl = XprotectAuth.NormalizeGatewayBaseUrl(gatewayBaseUrl),
            Username = username.Trim(),
            Password = password,
            ClientId = string.IsNullOrWhiteSpace(clientId) ? "GrantValidatorClient" : clientId.Trim()
        };
        var configError = XprotectAuth.Validate(options);
        if (configError is not null)
        {
            return configError;
        }

        using var handler = new HttpClientHandler();
        if (bypassSsl)
        {
            handler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }

        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };
        string? identityProvider = null;
        try
        {
            using var wellKnown = await client.GetAsync(
                $"{options.GatewayBaseUrl}/api/.well-known/uris",
                cancellationToken);
            if (wellKnown.IsSuccessStatusCode)
            {
                using var document = JsonDocument.Parse(await wellKnown.Content.ReadAsStringAsync(cancellationToken));
                if (document.RootElement.TryGetProperty("IdentityProvider", out var idp))
                {
                    identityProvider = idp.GetString();
                }
            }
        }
        catch
        {
            // Token URL fallbacks still run when well-known is blocked.
        }

        string? lastError = null;
        foreach (var tokenUrl in XprotectAuth.TokenUrlCandidates(options.GatewayBaseUrl, null, identityProvider))
        {
            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["username"] = options.Username,
                ["password"] = options.Password,
                ["client_id"] = options.ClientId
            });
            try
            {
                using var response = await client.PostAsync(tokenUrl, content, cancellationToken);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                if (response.IsSuccessStatusCode && body.Contains("access_token", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                var errorCode = XprotectAuth.ReadErrorCode(body);
                lastError = XprotectAuth.Explain((int)response.StatusCode, tokenUrl, body);
                if (!XprotectAuth.ShouldTryNextTokenUrl((int)response.StatusCode, errorCode))
                {
                    return lastError;
                }
            }
            catch (Exception ex)
            {
                lastError = $"Could not reach {tokenUrl}. {ex.Message}";
            }
        }

        return lastError ?? "XProtect login failed.";
    }
}
