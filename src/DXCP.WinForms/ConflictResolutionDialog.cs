using System.Diagnostics;
using System.Text;
using DevExpress.XtraEditors;

namespace DXCP.WinForms;

public class ConflictResolutionDialog : XtraForm
{
    private static readonly bool _vsCodeAvailable = IsCommandAvailable("code");
    private static readonly string? _ideaCommand = FindCommand("idea.cmd", "idea64.cmd", "idea", "idea64");

    private readonly GitService _gitService;
    private readonly List<string> _conflictedFiles;
    private readonly Dictionary<string, List<FilePart>> _parsedFiles = new();
    private readonly string _targetBranch;
    private readonly CommitInfo _commit;

    private readonly List<(string File, ConflictHunk Hunk)> _allHunks = new();
    private int _currentIndex;
    private string _currentFile = "";

    private LabelControl lblDescription = null!;
    private LabelControl lblFileInfo = null!;
    private LabelControl lblConflictNav = null!;
    private SimpleButton btnPrev = null!;
    private SimpleButton btnNext = null!;
    private MemoEdit memoEditor = null!;
    private SimpleButton btnKeepOurs = null!;
    private SimpleButton btnKeepTheirs = null!;
    private LabelControl labelStatus = null!;
    private SimpleButton btnContinue = null!;
    private SimpleButton btnAbort = null!;

    public bool Resolved { get; private set; }

    public ConflictResolutionDialog(GitService gitService, List<string> conflictedFiles, CommitInfo commit, string targetBranch)
    {
        _gitService = gitService;
        _conflictedFiles = conflictedFiles;
        _commit = commit;
        _targetBranch = targetBranch;
        ParseAllFiles();
        BuildHunkList();
        InitializeComponent();
        Shown += (s, e) => ShowCurrentConflict();
    }

    private void ParseAllFiles()
    {
        foreach (var file in _conflictedFiles)
        {
            var fullPath = Path.Combine(_gitService.WorkingDirectory, file);
            if (!File.Exists(fullPath)) continue;
            _parsedFiles[file] = ConflictParser.Parse(File.ReadAllText(fullPath));
        }
    }

    private void BuildHunkList()
    {
        foreach (var file in _conflictedFiles)
        {
            if (!_parsedFiles.ContainsKey(file)) continue;
            foreach (var part in _parsedFiles[file])
            {
                if (part.IsConflict && part.Hunk != null)
                    _allHunks.Add((file, part.Hunk));
            }
        }
    }

    private void InitializeComponent()
    {
        var commitMsg = _commit.Message.Split('\n')[0];
        if (commitMsg.Length > 60) commitMsg = commitMsg[..57] + "...";

        Text = $"Resolve Conflicts \u2014 {_commit.ShortSha} onto {_targetBranch}";
        Size = new System.Drawing.Size(1000, 700);
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MinimumSize = new System.Drawing.Size(700, 450);

        // === Top bar ===
        var topPanel = new Panel { Dock = DockStyle.Top, Height = 85, Padding = new Padding(12, 8, 12, 0) };

        lblDescription = new LabelControl
        {
            Text = $"Cherry-picking \"{commitMsg}\" ({_commit.ShortSha}) onto {_targetBranch}. For each conflict, choose which version to keep.",
            Dock = DockStyle.Top,
            AutoSizeMode = LabelAutoSizeMode.None,
            Height = 20,
            Appearance = { Font = new System.Drawing.Font("Segoe UI", 9F) }
        };

        lblFileInfo = new LabelControl
        {
            Dock = DockStyle.Top,
            AutoSizeMode = LabelAutoSizeMode.None,
            Height = 22,
            Appearance = { Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold) }
        };

        var navPanel = new Panel { Dock = DockStyle.Top, Height = 30 };

        btnPrev = new SimpleButton { Text = "<  Prev", Size = new System.Drawing.Size(80, 26), Dock = DockStyle.Left };
        btnPrev.Click += (s, e) => Navigate(-1);

        btnNext = new SimpleButton { Text = "Next  >", Size = new System.Drawing.Size(80, 26), Dock = DockStyle.Left };
        btnNext.Click += (s, e) => Navigate(1);

        lblConflictNav = new LabelControl
        {
            Dock = DockStyle.Left,
            AutoSizeMode = LabelAutoSizeMode.None,
            Size = new System.Drawing.Size(200, 26),
            Padding = new Padding(12, 0, 0, 0),
            Appearance = { Font = new System.Drawing.Font("Segoe UI", 9F) }
        };

        var navSpacer = new Panel { Dock = DockStyle.Left, Width = 8 };
        var toolTip = new ToolTip();

        var btnOpenIdea = new SimpleButton
        {
            Size = new System.Drawing.Size(30, 26),
            Dock = DockStyle.Right,
            Enabled = _ideaCommand != null,
            ImageOptions = { SvgImage = CreateSvgIcon(IdeaSvg), SvgImageSize = new System.Drawing.Size(18, 18) }
        };
        toolTip.SetToolTip(btnOpenIdea, _ideaCommand != null ? "Open in IntelliJ IDEA" : "IntelliJ IDEA not found in PATH");
        btnOpenIdea.Click += (s, e) => { if (_ideaCommand != null) OpenInEditor(_ideaCommand); };

        var editorSpacer = new Panel { Dock = DockStyle.Right, Width = 4 };

        var btnOpenVsCode = new SimpleButton
        {
            Size = new System.Drawing.Size(30, 26),
            Dock = DockStyle.Right,
            Enabled = _vsCodeAvailable,
            ImageOptions = { SvgImage = CreateSvgIcon(VsCodeSvg), SvgImageSize = new System.Drawing.Size(18, 18) }
        };
        toolTip.SetToolTip(btnOpenVsCode, _vsCodeAvailable ? "Open in VS Code" : "VS Code not found in PATH");
        btnOpenVsCode.Click += (s, e) => OpenInEditor("code");

        navPanel.Controls.Add(btnOpenIdea);
        navPanel.Controls.Add(editorSpacer);
        navPanel.Controls.Add(btnOpenVsCode);
        navPanel.Controls.Add(lblConflictNav);
        navPanel.Controls.Add(btnNext);
        navPanel.Controls.Add(navSpacer);
        navPanel.Controls.Add(btnPrev);

        topPanel.Controls.Add(navPanel);
        topPanel.Controls.Add(lblFileInfo);
        topPanel.Controls.Add(lblDescription);

        // === Bottom bar ===
        var bottomPanel = new Panel { Dock = DockStyle.Bottom, Height = 50, Padding = new Padding(12, 8, 12, 8) };

        btnKeepOurs = new SimpleButton
        {
            Text = $"Keep {_targetBranch}",
            Size = new System.Drawing.Size(140, 30),
            Dock = DockStyle.Left
        };
        btnKeepOurs.Click += (s, e) => ResolveCurrent("ours");

        var btnSpacer1 = new Panel { Dock = DockStyle.Left, Width = 8 };

        btnKeepTheirs = new SimpleButton
        {
            Text = "Keep Incoming",
            Size = new System.Drawing.Size(140, 30),
            Dock = DockStyle.Left
        };
        btnKeepTheirs.Click += (s, e) => ResolveCurrent("theirs");

        var btnSpacer2 = new Panel { Dock = DockStyle.Left, Width = 16 };

        labelStatus = new LabelControl
        {
            Dock = DockStyle.Left,
            AutoSizeMode = LabelAutoSizeMode.None,
            Size = new System.Drawing.Size(250, 30),
            Appearance = { Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold) }
        };

        btnAbort = new SimpleButton { Text = "Abort", Size = new System.Drawing.Size(80, 30), Dock = DockStyle.Right };
        btnAbort.Click += BtnAbort_Click;

        var btnSpacer3 = new Panel { Dock = DockStyle.Right, Width = 8 };

        btnContinue = new SimpleButton
        {
            Text = "Continue Cherry-Pick",
            Size = new System.Drawing.Size(150, 30),
            Dock = DockStyle.Right,
            Enabled = false
        };
        btnContinue.Click += BtnContinue_Click;

        bottomPanel.Controls.Add(labelStatus);
        bottomPanel.Controls.Add(btnSpacer2);
        bottomPanel.Controls.Add(btnKeepTheirs);
        bottomPanel.Controls.Add(btnSpacer1);
        bottomPanel.Controls.Add(btnKeepOurs);
        bottomPanel.Controls.Add(btnAbort);
        bottomPanel.Controls.Add(btnSpacer3);
        bottomPanel.Controls.Add(btnContinue);

        // === Center: single editor showing raw file ===
        var editorPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 4, 12, 4) };

        memoEditor = new MemoEdit { Dock = DockStyle.Fill };
        memoEditor.Properties.ReadOnly = true;
        memoEditor.Properties.WordWrap = false;
        memoEditor.Properties.ScrollBars = ScrollBars.Both;
        memoEditor.Font = new System.Drawing.Font("Consolas", 10F);

        editorPanel.Controls.Add(memoEditor);

        Controls.Add(editorPanel);
        Controls.Add(topPanel);
        Controls.Add(bottomPanel);
    }

    private void ShowCurrentConflict()
    {
        if (_allHunks.Count == 0) return;

        var (file, hunk) = _allHunks[_currentIndex];

        lblFileInfo.Text = file;
        lblConflictNav.Text = $"  Conflict {_currentIndex + 1} of {_allHunks.Count}";

        btnPrev.Enabled = _currentIndex > 0;
        btnNext.Enabled = _currentIndex < _allHunks.Count - 1;

        btnKeepOurs.Enabled = !hunk.IsResolved;
        btnKeepTheirs.Enabled = !hunk.IsResolved;

        _currentFile = file;
        RefreshEditorContent();
        ScrollToCurrentConflict(file, hunk);
        UpdateStatus();
    }

    private void RefreshEditorContent()
    {
        if (!_parsedFiles.ContainsKey(_currentFile)) return;

        var parts = _parsedFiles[_currentFile];
        var sb = new StringBuilder();

        foreach (var part in parts)
        {
            if (part.IsConflict && part.Hunk != null)
            {
                if (part.Hunk.IsResolved)
                {
                    sb.Append(part.Hunk.ResolvedContent);
                    if (!string.IsNullOrEmpty(part.Hunk.ResolvedContent))
                        sb.Append('\n');
                }
                else
                {
                    sb.Append($"<<<<<<< {_targetBranch}\n");
                    if (!string.IsNullOrEmpty(part.Hunk.OursContent))
                        sb.Append(part.Hunk.OursContent + "\n");
                    sb.Append("=======\n");
                    if (!string.IsNullOrEmpty(part.Hunk.TheirsContent))
                        sb.Append(part.Hunk.TheirsContent + "\n");
                    sb.Append($">>>>>>> {_commit.ShortSha}\n");
                }
            }
            else if (part.Text != null)
            {
                sb.Append(part.Text);
            }
        }

        memoEditor.Text = sb.ToString().Replace("\n", "\r\n").Replace("\r\r\n", "\r\n");
    }

    private void ScrollToCurrentConflict(string file, ConflictHunk hunk)
    {
        if (hunk.IsResolved) return;

        var markerText = $"<<<<<<< {_targetBranch}";
        var text = memoEditor.Text;

        // Find the Nth occurrence of the marker matching this hunk
        var hunksInFile = _parsedFiles[file]
            .Where(p => p.IsConflict && p.Hunk != null && !p.Hunk.IsResolved)
            .Select(p => p.Hunk)
            .ToList();
        var hunkOrder = hunksInFile.IndexOf(hunk);

        int pos = -1;
        int searchFrom = 0;
        for (int i = 0; i <= hunkOrder; i++)
        {
            pos = text.IndexOf(markerText, searchFrom, StringComparison.Ordinal);
            if (pos < 0) break;
            searchFrom = pos + 1;
        }

        if (pos < 0) return;

        BeginInvoke(new Action(() =>
        {
            memoEditor.Focus();
            memoEditor.SelectionStart = pos;
            memoEditor.SelectionLength = 0;
            memoEditor.ScrollToCaret();
        }));
    }

    private void Navigate(int direction)
    {
        _currentIndex = Math.Clamp(_currentIndex + direction, 0, _allHunks.Count - 1);
        ShowCurrentConflict();
    }

    private async void ResolveCurrent(string choice)
    {
        var (file, hunk) = _allHunks[_currentIndex];
        hunk.Resolution = choice;

        // Refresh the editor to show resolved content
        RefreshEditorContent();

        // Check if all hunks for this file are resolved
        var parts = _parsedFiles[file];
        var allFileHunksResolved = parts
            .Where(p => p.IsConflict && p.Hunk != null)
            .All(p => p.Hunk!.IsResolved);

        if (allFileHunksResolved)
        {
            var resolvedContent = ConflictParser.BuildResolved(parts);
            var fullPath = Path.Combine(_gitService.WorkingDirectory, file);
            await File.WriteAllTextAsync(fullPath, resolvedContent);
            await _gitService.StageFileAsync(file);
        }

        // Auto-advance to next unresolved
        var nextUnresolved = _allHunks.FindIndex(_currentIndex + 1, h => !h.Hunk.IsResolved);
        if (nextUnresolved >= 0)
        {
            _currentIndex = nextUnresolved;
        }

        ShowCurrentConflict();
    }

    private void UpdateStatus()
    {
        var resolved = _allHunks.Count(h => h.Hunk.IsResolved);
        var total = _allHunks.Count;
        labelStatus.Text = $"{resolved}/{total} conflicts resolved";
        btnContinue.Enabled = resolved == total && total > 0;
    }

    private void OpenInEditor(string command)
    {
        if (_allHunks.Count == 0) return;
        var (file, _) = _allHunks[_currentIndex];
        var fullPath = Path.Combine(_gitService.WorkingDirectory, file);

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = command,
                Arguments = $"\"{fullPath}\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            XtraMessageBox.Show($"Could not open editor: {ex.Message}", "Editor Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async void BtnContinue_Click(object? sender, EventArgs e)
    {
        btnContinue.Enabled = false;
        btnAbort.Enabled = false;

        var result = await _gitService.ContinueCherryPickAsync();
        if (result.Success)
        {
            Resolved = true;
            DialogResult = DialogResult.OK;
            Close();
        }
        else
        {
            XtraMessageBox.Show(
                $"Failed to continue cherry-pick:\n\n{result.Error}\n\n{result.Output}",
                "Cherry-Pick Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            btnContinue.Enabled = true;
            btnAbort.Enabled = true;
        }
    }

    private async void BtnAbort_Click(object? sender, EventArgs e)
    {
        var confirm = XtraMessageBox.Show(
            "Abort the cherry-pick? All conflict resolutions will be lost.",
            "Confirm Abort", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

        if (confirm == DialogResult.Yes)
        {
            await _gitService.AbortCherryPickAsync();
            Resolved = false;
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }

    private static string? FindCommand(params string[] candidates)
    {
        foreach (var cmd in candidates)
            if (IsCommandAvailable(cmd)) return cmd;
        return null;
    }

    private static bool IsCommandAvailable(string command)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "where",
                Arguments = command,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            process?.WaitForExit(3000);
            return process?.ExitCode == 0;
        }
        catch { return false; }
    }

    private static DevExpress.Utils.Svg.SvgImage CreateSvgIcon(string svg)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(svg));
        return DevExpress.Utils.Svg.SvgImage.FromStream(stream);
    }

    private const string VsCodeSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24">
          <path d="M17 2.5V21.5L7 17V7L17 2.5Z" fill="#007ACC"/>
          <path d="M7 7L2 12L7 17" stroke="#007ACC" stroke-width="2" fill="none"/>
          <path d="M17 2.5L7 12L17 21.5" stroke="#0065A9" stroke-width="1" fill="none"/>
        </svg>
        """;

    private const string IdeaSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24">
          <rect x="3" y="3" width="18" height="18" rx="2" fill="#087CFA"/>
          <rect x="6" y="6" width="12" height="12" rx="1" fill="#000"/>
          <rect x="8" y="8" width="2.5" height="8" fill="#fff"/>
          <rect x="12" y="8" width="2.5" height="8" fill="#fff"/>
          <rect x="12" y="8" width="5" height="2.5" fill="#fff"/>
        </svg>
        """;
}
