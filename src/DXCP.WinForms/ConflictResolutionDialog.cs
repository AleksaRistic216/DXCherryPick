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
    private readonly Dictionary<string, string> _lastKnownContent = new();
    private readonly Dictionary<string, string> _lineEndings = new();
    private readonly HashSet<string> _filesThatHadMarkers = new();
    private readonly string _targetBranch;
    private readonly CommitInfo _commit;

    private readonly List<(string File, ConflictHunk Hunk)> _allHunks = new();
    private List<string> _unmergedFiles = new();
    private int _initialConflictCount;
    private int _currentIndex;
    private string _currentFile = "";
    private bool _syncing;
    private bool _uiReady;
    private bool _dirty;
    private bool _suppressTextChanged;
    private bool _closeApproved;
    private bool _closing;
    private bool _suppressScrollToConflict;

    private LabelControl lblDescription = null!;
    private LabelControl lblFileInfo = null!;
    private LabelControl lblConflictNav = null!;
    private SimpleButton btnPrev = null!;
    private SimpleButton btnNext = null!;
    private MemoEdit memoEditor = null!;
    private SimpleButton btnKeepOurs = null!;
    private SimpleButton btnKeepTheirs = null!;
    private SimpleButton btnSave = null!;
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
        _unmergedFiles = new List<string>(conflictedFiles);
        ParseAllFiles();
        BuildHunkList();
        _initialConflictCount = _allHunks.Count;
        InitializeComponent();
        Shown += (s, e) =>
        {
            _uiReady = true;
            ShowCurrentConflict();
        };
        // Never auto-sync over unsaved editor changes — the user's typing wins until they save or reload.
        Activated += async (s, e) => { if (_uiReady && !_dirty && !_closing) await SyncWithDiskAsync(false); };
        FormClosing += ConflictResolutionDialog_FormClosing;
    }

    /// <summary>
    /// Leaving without continuing means the cherry-pick has to be abandoned, otherwise the worktree
    /// is left mid-cherry-pick with unmerged files and the caller cannot switch branches. The window
    /// close button, Alt+F4 and the Abort button all land here so there is one implementation.
    /// </summary>
    private void ConflictResolutionDialog_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (Resolved || _closeApproved) return;

        // Don't hold up an application or Windows shutdown for a confirmation prompt.
        if (e.CloseReason is not (CloseReason.UserClosing or CloseReason.None)) return;

        // The abort has to finish before the dialog goes away, and FormClosing cannot await.
        e.Cancel = true;
        BeginInvoke(new Action(async () => await AbortAndCloseAsync()));
    }

    private async Task AbortAndCloseAsync()
    {
        if (_closing) return;
        _closing = true;
        try
        {
            if (await _gitService.IsCherryPickInProgressAsync())
            {
                var warning = _dirty
                    ? "Abort the cherry-pick? Your unsaved edits and all conflict resolutions will be lost."
                    : "Abort the cherry-pick? All conflict resolutions will be lost.";

                if (XtraMessageBox.Show(warning, "Confirm Abort",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;

                var result = await _gitService.AbortCherryPickAsync();
                if (!result.Success && await _gitService.IsCherryPickInProgressAsync())
                {
                    XtraMessageBox.Show(
                        $"Could not abort the cherry-pick:\n\n{result.Error}\n\n" +
                        "The worktree is still mid-cherry-pick — resolve the conflicts or run " +
                        "'git cherry-pick --abort' manually.",
                        "Abort Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            Resolved = false;
            _closeApproved = true;
            DialogResult = DialogResult.Cancel;
            Close();
        }
        finally
        {
            _closing = false;
        }
    }

    private void ParseAllFiles()
    {
        foreach (var file in _conflictedFiles)
        {
            var fullPath = Path.Combine(_gitService.WorkingDirectory, file);
            if (!File.Exists(fullPath)) continue;
            var content = File.ReadAllText(fullPath);
            _lastKnownContent[file] = content;
            _parsedFiles[file] = Parse(file, content);
        }
    }

    private List<FilePart> Parse(string file, string content)
    {
        _lineEndings[file] = DetectLineEnding(content);
        var parts = ConflictParser.Parse(content);
        if (parts.Any(p => p.IsConflict))
            _filesThatHadMarkers.Add(file);
        return parts;
    }

    private static string DetectLineEnding(string content)
    {
        var crlf = content.Split("\r\n").Length - 1;
        var lf = content.Count(c => c == '\n');
        return crlf > lf - crlf ? "\r\n" : "\n";
    }

    /// <summary>
    /// Converts editor text (always CRLF) back to the line endings the file uses on disk, so
    /// resolving a conflict never rewrites every line of a LF file.
    /// </summary>
    private string ToFileText(string file, string text)
    {
        var normalized = text.Replace("\r\n", "\n").Replace("\r", "\n");
        return _lineEndings.TryGetValue(file, out var ending) && ending == "\r\n"
            ? normalized.Replace("\n", "\r\n")
            : normalized;
    }

    /// <summary>
    /// Writes the editor's text to the current file, then re-parses it so the hunk list, staging
    /// and Continue state reflect what was just typed.
    /// </summary>
    private async Task<bool> SaveEditorAsync()
    {
        if (!_dirty) return true;
        if (string.IsNullOrEmpty(_currentFile)) return false;

        var file = _currentFile;
        var content = ToFileText(file, memoEditor.Text);
        var fullPath = Path.Combine(_gitService.WorkingDirectory, file);

        try
        {
            await File.WriteAllTextAsync(fullPath, content);
        }
        catch (Exception ex)
        {
            XtraMessageBox.Show($"Could not save {file}:\n\n{ex.Message}", "Save Failed",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }

        _dirty = false;
        _lastKnownContent[file] = content;
        _parsedFiles[file] = Parse(file, content);

        var caret = memoEditor.SelectionStart;
        await RefreshUnmergedFilesAsync();

        // Rebuilding the view must not yank the caret back to the conflict marker mid-edit.
        _suppressScrollToConflict = true;
        try
        {
            RebuildHunkList();
            ShowCurrentConflict();
        }
        finally
        {
            _suppressScrollToConflict = false;
        }

        BeginInvoke(new Action(() =>
        {
            memoEditor.SelectionStart = Math.Min(caret, memoEditor.Text.Length);
            memoEditor.SelectionLength = 0;
            memoEditor.ScrollToCaret();
        }));

        return true;
    }

    private async Task ReloadRequestedAsync()
    {
        if (_dirty)
        {
            var confirm = XtraMessageBox.Show(
                "Discard your unsaved edits and re-read the files from disk?",
                "Discard Edits", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) return;

            _dirty = false;
            ShowCurrentConflict(); // restore the editor from the last known file content
        }

        await SyncWithDiskAsync(true);
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

    /// <summary>
    /// Re-reads the conflicted files from disk so resolutions made in an external editor are
    /// picked up, stages whatever no longer has conflict markers, and refreshes the UI.
    /// Files whose content is unchanged keep their in-dialog resolutions.
    /// </summary>
    private async Task SyncWithDiskAsync(bool manual)
    {
        if (_syncing || _closing || IsDisposed) return;
        _syncing = true;
        try
        {
            var changedOnDisk = false;

            foreach (var file in _conflictedFiles)
            {
                var fullPath = Path.Combine(_gitService.WorkingDirectory, file);
                if (!File.Exists(fullPath)) continue;

                string content;
                try
                {
                    content = await File.ReadAllTextAsync(fullPath);
                }
                catch (IOException)
                {
                    continue; // editor may still be writing — try again on the next sync
                }

                if (_lastKnownContent.TryGetValue(file, out var known) && known == content)
                    continue;

                _lastKnownContent[file] = content;
                _parsedFiles[file] = Parse(file, content);
                changedOnDisk = true;
            }

            await RefreshUnmergedFilesAsync();

            // The dialog may have been closed while the git calls above were running.
            if (IsDisposed || Disposing) return;

            if (changedOnDisk)
            {
                RebuildHunkList();
                ShowCurrentConflict();
            }
            else
            {
                UpdateStatus();
                if (manual) labelStatus.Text += "  (no changes on disk)";
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    /// <summary>
    /// Asks git which files are still unmerged, staging any whose conflict markers have been
    /// resolved away outside this dialog. Files that never had markers (binary or modify/delete
    /// conflicts) are left alone — staging those is a decision only the user can make.
    /// </summary>
    private async Task RefreshUnmergedFilesAsync()
    {
        _unmergedFiles = await _gitService.GetConflictedFilesAsync();

        var stagedAny = false;
        foreach (var file in _unmergedFiles)
        {
            if (!_filesThatHadMarkers.Contains(file)) continue;
            if (!_parsedFiles.TryGetValue(file, out var parts)) continue;
            if (parts.Any(p => p.IsConflict)) continue;
            await _gitService.StageFileAsync(file);
            stagedAny = true;
        }

        if (stagedAny)
            _unmergedFiles = await _gitService.GetConflictedFilesAsync();
    }

    private void RebuildHunkList()
    {
        var previousFile = _currentFile;

        _allHunks.Clear();
        BuildHunkList();

        if (_allHunks.Count == 0)
        {
            _currentIndex = 0;
            return;
        }

        var index = _allHunks.FindIndex(h => h.File == previousFile && !h.Hunk.IsResolved);
        if (index < 0) index = _allHunks.FindIndex(h => !h.Hunk.IsResolved);
        if (index < 0) index = _allHunks.FindIndex(h => h.File == previousFile);
        _currentIndex = Math.Clamp(index < 0 ? 0 : index, 0, _allHunks.Count - 1);
    }

    private void InitializeComponent()
    {
        var commitMsg = _commit.Message.Split('\n')[0];
        if (commitMsg.Length > 60) commitMsg = commitMsg[..57] + "...";

        Text = $"Resolve Conflicts \u2014 {_commit.ShortSha} onto {_targetBranch}";
        Size = new System.Drawing.Size(1000, 700);
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MinimumSize = new System.Drawing.Size(860, 450);
        KeyPreview = true;
        KeyDown += async (s, e) =>
        {
            if (e.KeyCode == Keys.F5)
            {
                e.Handled = true;
                await ReloadRequestedAsync();
            }
            else if (e.Control && e.KeyCode == Keys.S)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                await SaveEditorAsync();
            }
        };

        // === Top bar ===
        var topPanel = new Panel { Dock = DockStyle.Top, Height = 85, Padding = new Padding(12, 8, 12, 0) };

        lblDescription = new LabelControl
        {
            Text = $"Cherry-picking \"{commitMsg}\" ({_commit.ShortSha}) onto {_targetBranch}. Keep one side, or edit the file below and press Ctrl+S.",
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
        btnPrev.Click += async (s, e) => await NavigateAsync(-1);

        btnNext = new SimpleButton { Text = "Next  >", Size = new System.Drawing.Size(80, 26), Dock = DockStyle.Left };
        btnNext.Click += async (s, e) => await NavigateAsync(1);

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
        btnOpenIdea.Click += async (s, e) => { if (_ideaCommand != null) await OpenInEditorAsync(_ideaCommand); };

        var editorSpacer = new Panel { Dock = DockStyle.Right, Width = 4 };

        var btnOpenVsCode = new SimpleButton
        {
            Size = new System.Drawing.Size(30, 26),
            Dock = DockStyle.Right,
            Enabled = _vsCodeAvailable,
            ImageOptions = { SvgImage = CreateSvgIcon(VsCodeSvg), SvgImageSize = new System.Drawing.Size(18, 18) }
        };
        toolTip.SetToolTip(btnOpenVsCode, _vsCodeAvailable ? "Open in VS Code" : "VS Code not found in PATH");
        btnOpenVsCode.Click += async (s, e) => await OpenInEditorAsync("code");

        var reloadSpacer = new Panel { Dock = DockStyle.Right, Width = 8 };

        var btnReload = new SimpleButton
        {
            Text = "Reload",
            Size = new System.Drawing.Size(80, 26),
            Dock = DockStyle.Right
        };
        toolTip.SetToolTip(btnReload, "Re-read the files from disk to pick up edits made outside this dialog (F5)");
        btnReload.Click += async (s, e) => await ReloadRequestedAsync();

        navPanel.Controls.Add(btnOpenIdea);
        navPanel.Controls.Add(editorSpacer);
        navPanel.Controls.Add(btnOpenVsCode);
        navPanel.Controls.Add(reloadSpacer);
        navPanel.Controls.Add(btnReload);
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

        btnSave = new SimpleButton
        {
            Text = "Save",
            Size = new System.Drawing.Size(80, 30),
            Dock = DockStyle.Left,
            Enabled = false
        };
        toolTip.SetToolTip(btnSave, "Write your edits to the file on disk (Ctrl+S)");
        btnSave.Click += async (s, e) => await SaveEditorAsync();

        var btnSpacer4 = new Panel { Dock = DockStyle.Left, Width = 16 };

        labelStatus = new LabelControl
        {
            Dock = DockStyle.Left,
            AutoSizeMode = LabelAutoSizeMode.None,
            Size = new System.Drawing.Size(300, 30),
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
        bottomPanel.Controls.Add(btnSpacer4);
        bottomPanel.Controls.Add(btnSave);
        bottomPanel.Controls.Add(btnSpacer2);
        bottomPanel.Controls.Add(btnKeepTheirs);
        bottomPanel.Controls.Add(btnSpacer1);
        bottomPanel.Controls.Add(btnKeepOurs);
        bottomPanel.Controls.Add(btnAbort);
        bottomPanel.Controls.Add(btnSpacer3);
        bottomPanel.Controls.Add(btnContinue);

        // === Center: editable view of the raw file ===
        var editorPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 4, 12, 4) };

        memoEditor = new MemoEdit { Dock = DockStyle.Fill };
        memoEditor.Properties.WordWrap = false;
        memoEditor.Properties.ScrollBars = ScrollBars.Both;
        memoEditor.Properties.AcceptsTab = true;
        memoEditor.Font = new System.Drawing.Font("Consolas", 10F);
        memoEditor.TextChanged += (s, e) =>
        {
            if (_suppressTextChanged || _dirty) return;
            _dirty = true;
            UpdateStatus();
        };

        editorPanel.Controls.Add(memoEditor);

        Controls.Add(editorPanel);
        Controls.Add(topPanel);
        Controls.Add(bottomPanel);
    }

    private void ShowCurrentConflict()
    {
        if (_allHunks.Count == 0)
        {
            if (!_parsedFiles.ContainsKey(_currentFile))
                _currentFile = _parsedFiles.Keys.FirstOrDefault() ?? "";

            lblFileInfo.Text = _currentFile;
            lblConflictNav.Text = "  No conflict markers left";
            btnPrev.Enabled = false;
            btnNext.Enabled = false;
            btnKeepOurs.Enabled = false;
            btnKeepTheirs.Enabled = false;
            RefreshEditorContent();
            UpdateStatus();
            return;
        }

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

        _suppressTextChanged = true;
        memoEditor.Text = sb.ToString().Replace("\n", "\r\n").Replace("\r\r\n", "\r\n");
        _suppressTextChanged = false;
        _dirty = false;
    }

    private void ScrollToCurrentConflict(string file, ConflictHunk hunk)
    {
        if (hunk.IsResolved || _suppressScrollToConflict) return;

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

    private async Task NavigateAsync(int direction)
    {
        // Moving on commits what was typed, so edits survive the jump.
        if (!await SaveEditorAsync()) return;
        if (_allHunks.Count == 0) return;

        _currentIndex = Math.Clamp(_currentIndex + direction, 0, _allHunks.Count - 1);
        ShowCurrentConflict();
    }

    private async void ResolveCurrent(string choice)
    {
        if (_dirty || _allHunks.Count == 0) return;

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
            var resolvedContent = ToFileText(file, ConflictParser.BuildResolved(parts));
            var fullPath = Path.Combine(_gitService.WorkingDirectory, file);
            await File.WriteAllTextAsync(fullPath, resolvedContent);
            _lastKnownContent[file] = resolvedContent;
            await _gitService.StageFileAsync(file);
            await RefreshUnmergedFilesAsync();
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
        btnSave.Enabled = _dirty;

        if (_dirty)
        {
            // Keeping a side would rebuild the text from the parsed model and throw the edits away.
            labelStatus.Text = "Unsaved edits — press Ctrl+S to save";
            btnKeepOurs.Enabled = false;
            btnKeepTheirs.Enabled = false;
            btnContinue.Enabled = false;
            return;
        }

        // git is the authority on what is still unmerged: conflicts can also be resolved by hand
        // or in another editor, in which case no hunks are left to count.
        var unresolved = _allHunks.Count(h => !h.Hunk.IsResolved);
        var total = Math.Max(_initialConflictCount, unresolved);
        var ready = unresolved == 0 && _unmergedFiles.Count == 0;

        if (unresolved == 0 && _unmergedFiles.Count > 0)
            labelStatus.Text = $"{_unmergedFiles.Count} file(s) still unmerged — stage or resolve them";
        else
            labelStatus.Text = $"{total - unresolved}/{total} conflicts resolved";

        btnContinue.Enabled = ready;
    }

    private async Task OpenInEditorAsync(string command)
    {
        // Hand the editor the text the user is looking at, not a stale copy.
        if (!await SaveEditorAsync()) return;

        if (string.IsNullOrEmpty(_currentFile)) return;
        var fullPath = Path.Combine(_gitService.WorkingDirectory, _currentFile);

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
        // The files may have changed since the last sync — verify before committing.
        if (!await SaveEditorAsync()) return;
        await SyncWithDiskAsync(false);
        await RefreshUnmergedFilesAsync(); // in case a sync was already in flight and skipped

        var unresolved = _allHunks.Count(h => !h.Hunk.IsResolved);
        if (unresolved > 0 || _unmergedFiles.Count > 0)
        {
            var detail = _unmergedFiles.Count > 0
                ? "Still unmerged:\n\n" + string.Join("\n", _unmergedFiles.Select(f => "  " + f))
                : $"{unresolved} conflict(s) still contain conflict markers.";
            XtraMessageBox.Show(
                $"Cannot continue yet.\n\n{detail}",
                "Conflicts Remaining", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        btnContinue.Enabled = false;
        btnAbort.Enabled = false;
        _closing = true; // no background refresh should race the commit and the close

        var result = await _gitService.ContinueCherryPickAsync();
        if (result.Success)
        {
            Resolved = true;
            _closeApproved = true;
            DialogResult = DialogResult.OK;
            Close();
        }
        else
        {
            XtraMessageBox.Show(
                $"Failed to continue cherry-pick:\n\n{result.Error}\n\n{result.Output}",
                "Cherry-Pick Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _closing = false;
            btnAbort.Enabled = true;
            await RefreshUnmergedFilesAsync();
            UpdateStatus();
        }
    }

    private async void BtnAbort_Click(object? sender, EventArgs e)
    {
        await AbortAndCloseAsync();
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
