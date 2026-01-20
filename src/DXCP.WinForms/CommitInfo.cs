namespace DXCP.WinForms;

public class CommitInfo
{
    public string Sha { get; set; } = string.Empty;
    public string ShortSha => Sha.Length >= 7 ? Sha[..7] : Sha;
    public string Message { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public DateTime Date { get; set; }
}
