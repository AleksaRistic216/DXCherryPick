# DXCherryPick

Tools for cherry-picking GitHub pull requests to versioned branches in the DevExpress organization.

- **`DXCP.WinForms.exe`** — GUI for browsing your PRs and launching cherry-picks interactively
- **`cp.exe`** — CLI for cherry-picking a PR directly from the terminal

## Prerequisites

- Windows
- [.NET 10 Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)
- DevExpress NuGet feed configured (v26.1+)
- Git on `PATH`
- GitHub Personal Access Token with SSO authorized for the DevExpress org

## Build

```bash
bash build.sh
```

Output lands in `./release/`.

## GUI

```bash
./release/DXCP.WinForms.exe
```

On first run you will be prompted for your GitHub PAT. It is stored securely in Windows Credential Manager and reused on subsequent starts and by the CLI.

The grid shows your open and recently updated PRs. Right-click a row and select **Cherry-pick** to start the interactive flow: pick target branches, review commits, resolve any conflicts.

### Resolving conflicts

The conflict dialog offers three ways to settle each conflict, and they can be mixed freely:

- **Keep 〈branch〉 / Keep Incoming** — take one side of the hunk wholesale.
- **Edit in place** — the file is editable right in the dialog; press **Ctrl+S** (or **Save**) to write it out.
- **Your own editor** — open the file in VS Code or IntelliJ from the toolbar. The dialog re-reads the file when it regains focus, or on **Reload** / **F5**.

Once a file has no conflict markers left it is staged automatically, and **Continue Cherry-Pick** unlocks when git reports nothing unmerged.

Leaving the dialog any other way — **Abort**, the window close button or Alt+F4 — confirms first and then runs `git cherry-pick --abort`, so the worktree is never left mid-cherry-pick.

## CLI

```bash
./release/cp.exe <pr-number> [<branch>...] [options]
```

Cherry-picks a PR to one or more versioned branches non-interactively. Run from inside any branch worktree of the target repository — the repo is inferred from `git remote get-url origin` and the worktree parent from the folder above the current directory (or from the saved GUI config).

### Examples

```bash
# Auto-detect target branches (versioned branches excluding the PR's base)
cp 12345

# Cherry-pick to specific branches
cp 12345 2025.2 2025.1

# Override repo and worktree parent
cp 12345 --repo DevExpress/dxvcs --worktree-parent C:\repos\dxvcs

# Force-push (useful when re-running)
cp 12345 2025.2 --force-push
```

### Options

| Option | Description |
|---|---|
| `--repo owner/repo` | Override repo (default: inferred from `git remote`) |
| `--worktree-parent <path>` | Parent folder containing branch worktrees (default: GUI config or parent of CWD) |
| `--token <pat>` | GitHub PAT (overrides `GITHUB_TOKEN` env var and Credential Manager) |
| `--force-push` | Force-push cherry-pick branches |

### Token resolution

The CLI looks for a token in this order:

1. `--token` flag
2. `GITHUB_TOKEN` environment variable
3. Windows Credential Manager (same credential saved by the GUI)

### Worktree layout

The CLI expects branch worktrees as sibling folders under a common parent:

```
C:\repos\dxvcs\
    2026.1\    ← git worktree on branch 2026.1
    2025.2\    ← git worktree on branch 2025.2
    2025.1\    ← git worktree on branch 2025.1
```

Run `cp` from any one of these folders; it will cherry-pick into the others.

### Exit codes

| Code | Meaning |
|---|---|
| `0` | All target branches succeeded |
| `1` | One or more branches failed (details printed to stderr) |

Conflicts abort the cherry-pick for that branch and report an error — use the GUI for interactive conflict resolution.

## GitHub PAT setup

1. Go to **GitHub → Settings → Developer settings → Personal access tokens → Tokens (classic)**
2. Click **Generate new token (classic)**
3. Give it a name (e.g. `DXCherryPick`) and select the **`repo`** scope
4. Click **Generate token** and copy the value — GitHub only shows it once
5. **Authorize SSO for the DevExpress organization:**
   - Find the token in the token list and click **Configure SSO**
   - Click **Authorize** next to **DevExpress** and complete the SSO sign-in

> **Why SSO is required:** The DevExpress org enforces SAML single sign-on. A PAT without SSO authorization will authenticate successfully but return empty results for org repositories and PRs.
