using Microsoft.AspNetCore.DataProtection;

namespace Milestone.Dashboard.Services;

public static class AppSecretProtector
{
    public const string Prefix = "ENC:";
    public const string Purpose = "Milestone.Dashboard.Password.v1";

    public static bool IsProtected(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.StartsWith(Prefix, StringComparison.Ordinal);

    public static string Protect(IDataProtector protector, string plain)
    {
        if (string.IsNullOrEmpty(plain) || IsProtected(plain))
        {
            return plain;
        }

        return Prefix + protector.Protect(plain);
    }

    public static string Unprotect(IDataProtector protector, string value)
    {
        if (!IsProtected(value))
        {
            return value;
        }

        return protector.Unprotect(value[Prefix.Length..]);
    }

    public static string KeysDirectory(string root) =>
        Path.Combine(root, "App_Data", "keys");
}
