using System.Text.Json;
using System.Text.Json.Nodes;

namespace Milestone.Dashboard.Services;

public sealed class AppSettingsPasswordWriter
{
    private readonly string _path;

    public AppSettingsPasswordWriter(IWebHostEnvironment environment)
        : this(System.IO.Path.Combine(environment.ContentRootPath, "appsettings.json"))
    {
    }

    internal AppSettingsPasswordWriter(string path)
    {
        _path = path;
    }

    public string FilePath => _path;

    public bool FileExists => File.Exists(_path);

    public bool CanWrite
    {
        get
        {
            try
            {
                if (!FileExists)
                {
                    return false;
                }

                using var stream = File.Open(_path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
                return stream.CanWrite;
            }
            catch
            {
                return false;
            }
        }
    }

    public bool PasswordIsEncrypted
    {
        get
        {
            try
            {
                return AppSecretProtector.IsProtected(ReadPassword());
            }
            catch
            {
                return false;
            }
        }
    }

    public string? ReadPassword()
    {
        if (!FileExists)
        {
            return null;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(_path));
        return document.RootElement.TryGetProperty("Milestone", out var milestone)
               && milestone.TryGetProperty("Password", out var password)
            ? password.GetString()
            : null;
    }

    public void SaveEncrypted(string encrypted)
    {
        if (!encrypted.StartsWith(AppSecretProtector.Prefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The value to save was not an ENC: password.");
        }

        var json = File.ReadAllText(_path);
        var node = JsonNode.Parse(json) ?? throw new InvalidOperationException("appsettings.json is empty.");
        var milestone = node["Milestone"] as JsonObject
                        ?? throw new InvalidOperationException("appsettings.json has no Milestone section.");
        milestone["Password"] = encrypted;
        File.Copy(_path, _path + ".bak", overwrite: true);
        File.WriteAllText(_path, node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }
}
