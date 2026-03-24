using DevExpress.XtraEditors;

namespace DXCP.WinForms;

public class CherryPickDialog : XtraForm
{
    private LabelControl labelPrTitle = null!;
    private LabelControl labelCommitsHeader = null!;
    private ListBoxControl listBoxCommits = null!;
    private LabelControl labelBranchesHeader = null!;
    private CheckedListBoxControl checkedListBoxBranches = null!;
    private CheckEdit checkForcePush = null!;
    private SimpleButton btnOk = null!;
    private SimpleButton btnCancel = null!;

    public List<string> SelectedBranches { get; private set; } = new();
    public bool ForcePush { get; private set; }

    public CherryPickDialog(PullRequest pr, List<CommitInfo> commits, List<string> targetBranches)
    {
        InitializeComponent();
        PopulateData(pr, commits, targetBranches);
    }

    private void InitializeComponent()
    {
        Text = "Cherry Pick";
        Size = new System.Drawing.Size(500, 480);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        labelPrTitle = new LabelControl
        {
            Location = new System.Drawing.Point(12, 12),
            AutoSizeMode = LabelAutoSizeMode.None,
            Size = new System.Drawing.Size(460, 40),
            Appearance = { Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold) }
        };

        labelCommitsHeader = new LabelControl
        {
            Location = new System.Drawing.Point(12, 60),
            Text = "Commits to cherry-pick:"
        };

        listBoxCommits = new ListBoxControl
        {
            Location = new System.Drawing.Point(12, 80),
            Size = new System.Drawing.Size(460, 120)
        };

        labelBranchesHeader = new LabelControl
        {
            Location = new System.Drawing.Point(12, 210),
            Text = "Select target branches:"
        };

        checkedListBoxBranches = new CheckedListBoxControl
        {
            Location = new System.Drawing.Point(12, 230),
            Size = new System.Drawing.Size(460, 120),
            CheckOnClick = true
        };

        checkForcePush = new CheckEdit
        {
            Text = "Force push (cherry-pick branch, useful if it already exists from previous failed attempt)",
            Location = new System.Drawing.Point(12, 360),
            Size = new System.Drawing.Size(460, 20)
        };

        btnOk = new SimpleButton
        {
            Text = "Cherry Pick",
            Location = new System.Drawing.Point(290, 400),
            Size = new System.Drawing.Size(90, 30),
            DialogResult = DialogResult.OK
        };
        btnOk.Click += BtnOk_Click;

        btnCancel = new SimpleButton
        {
            Text = "Cancel",
            Location = new System.Drawing.Point(385, 400),
            Size = new System.Drawing.Size(90, 30),
            DialogResult = DialogResult.Cancel
        };

        Controls.AddRange(new Control[]
        {
            labelPrTitle,
            labelCommitsHeader,
            listBoxCommits,
            labelBranchesHeader,
            checkedListBoxBranches,
            checkForcePush,
            btnOk,
            btnCancel
        });

        AcceptButton = btnOk;
        CancelButton = btnCancel;
    }

    private void PopulateData(PullRequest pr, List<CommitInfo> commits, List<string> targetBranches)
    {
        labelPrTitle.Text = $"PR #{pr.Number}: {pr.Title}";

        foreach (var commit in commits)
        {
            var firstLine = commit.Message.Split('\n')[0];
            var displayText = $"{commit.ShortSha} - {firstLine}";
            if (displayText.Length > 80)
                displayText = displayText[..77] + "...";
            listBoxCommits.Items.Add(displayText);
        }

        foreach (var branch in targetBranches)
        {
            checkedListBoxBranches.Items.Add(branch);
        }
    }

    private void BtnOk_Click(object? sender, EventArgs e)
    {
        SelectedBranches = checkedListBoxBranches.CheckedItems
            .Cast<object>()
            .Select(item => item.ToString() ?? string.Empty)
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();

        if (SelectedBranches.Count == 0)
        {
            XtraMessageBox.Show(
                "Please select at least one target branch.",
                "No Branch Selected",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }

        ForcePush = checkForcePush.Checked;
    }
}
