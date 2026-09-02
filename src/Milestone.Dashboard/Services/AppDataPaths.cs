using Microsoft.Extensions.Logging;

namespace Milestone.Dashboard.Services;

public static class AppDataPaths
{
    public const string FolderName = "App_Data";

    public const string GrantHint =
        @"Grant Modify on C:\inetpub\xprotect-dashboard\App_Data to IIS AppPool\XProtectDashboard. " +
        @"On the IIS server, run scripts\\grant-app-data-access.ps1 as Administrator. " +
        @"Do not use C:\\Windows\\TEMP.";

    public static IReadOnlyList<string> CandidateFolders(IWebHostEnvironment environment)
    {
        var folders = new List<string>();
        if (!string.IsNullOrWhiteSpace(environment.ContentRootPath))
        {
            folders.Add(Path.Combine(environment.ContentRootPath, FolderName));
        }

        var webRoot = environment.WebRootPath ?? environment.ContentRootPath;
        if (!string.IsNullOrWhiteSpace(webRoot))
        {
            folders.Add(Path.Combine(webRoot, "..", FolderName));
        }

        return folders
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string? TryGetWritableDirectory(IWebHostEnvironment environment, ILogger? logger = null)
    {
        foreach (var folder in CandidateFolders(environment))
        {
            if (TryEnsureWritable(folder, logger))
            {
                return folder;
            }
        }

        logger?.LogError("IIS cannot write App_Data under the site folder. {Hint}", GrantHint);
        return null;
    }

    public static string? CombineFile(IWebHostEnvironment environment, string fileName, ILogger? logger = null)
    {
        var directory = TryGetWritableDirectory(environment, logger);
        return directory is null ? null : Path.Combine(directory, fileName);
    }

    public static bool TryEnsureWritable(string folder, ILogger? logger = null)
    {
        try
        {
            Directory.CreateDirectory(folder);
            var probe = Path.Combine(folder, $".write-{Guid.NewGuid():N}");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            logger?.LogWarning(ex, "Could not use data folder {Folder}", folder);
            return false;
        }
    }
}
