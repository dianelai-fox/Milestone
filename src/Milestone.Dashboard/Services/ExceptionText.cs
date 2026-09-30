namespace Milestone.Dashboard.Services;

public static class ExceptionText
{
    public static string Flatten(Exception? error)
    {
        if (error is null)
        {
            return string.Empty;
        }

        var parts = new List<string>();
        for (var current = error; current is not null; current = current.InnerException)
        {
            var text = current.Message.ReplaceLineEndings(" ").Trim();
            if (text.Length == 0)
            {
                continue;
            }

            if (parts.Exists(part => part.Contains(text, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            parts.Add(text);
        }

        return string.Join(" ", parts);
    }
}
