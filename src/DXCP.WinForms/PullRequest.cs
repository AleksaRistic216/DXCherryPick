namespace DXCP.WinForms;

public class PullRequest
{
    public int Number { get; set; }
    public string Title { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Repository { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string Url { get; set; } = string.Empty;
    public string BaseBranch { get; set; } = string.Empty;
    public string HeadBranch { get; set; } = string.Empty;
    public bool IsDraft { get; set; }
    public int Additions { get; set; }
    public int Deletions { get; set; }
}
