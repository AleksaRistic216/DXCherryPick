using System.Text;

namespace DXCP.WinForms;

public class ConflictHunk
{
    public string OursContent { get; set; } = string.Empty;
    public string TheirsContent { get; set; } = string.Empty;
    public string? Resolution { get; set; }
    public bool IsResolved => Resolution != null;
    public string ResolvedContent => Resolution == "ours" ? OursContent : TheirsContent;
}

public class FilePart
{
    public bool IsConflict { get; set; }
    public string? Text { get; set; }
    public ConflictHunk? Hunk { get; set; }
}

public static class ConflictParser
{
    public static List<FilePart> Parse(string content)
    {
        var parts = new List<FilePart>();
        var normalized = content.Replace("\r\n", "\n");
        var lines = normalized.Split('\n');
        var textLines = new List<string>();
        var oursLines = new List<string>();
        var theirsLines = new List<string>();
        var inConflict = false;
        var inOurs = false;

        foreach (var line in lines)
        {
            if (line.StartsWith("<<<<<<<"))
            {
                if (textLines.Count > 0)
                {
                    parts.Add(new FilePart { Text = string.Join("\n", textLines) + "\n" });
                    textLines.Clear();
                }
                inConflict = true;
                inOurs = true;
                oursLines.Clear();
                theirsLines.Clear();
            }
            else if (inConflict && line.StartsWith("======="))
            {
                inOurs = false;
            }
            else if (inConflict && line.StartsWith(">>>>>>>"))
            {
                parts.Add(new FilePart
                {
                    IsConflict = true,
                    Hunk = new ConflictHunk
                    {
                        OursContent = string.Join("\n", oursLines),
                        TheirsContent = string.Join("\n", theirsLines)
                    }
                });
                inConflict = false;
            }
            else if (inConflict)
            {
                if (inOurs) oursLines.Add(line);
                else theirsLines.Add(line);
            }
            else
            {
                textLines.Add(line);
            }
        }

        if (textLines.Count > 0)
            parts.Add(new FilePart { Text = string.Join("\n", textLines) });

        return parts;
    }

    public static string BuildResolved(List<FilePart> parts)
    {
        var sb = new StringBuilder();
        foreach (var part in parts)
        {
            if (part.IsConflict && part.Hunk != null)
            {
                var content = part.Hunk.ResolvedContent;
                if (!string.IsNullOrEmpty(content))
                {
                    sb.Append(content);
                    sb.Append('\n');
                }
            }
            else if (part.Text != null)
            {
                sb.Append(part.Text);
            }
        }
        return sb.ToString();
    }
}
