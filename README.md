# DXCherryPick

Windows Forms app for browsing your GitHub pull requests in the DevExpress organization and cherry-picking them to versioned branches.

## Prerequisites

- Windows
- [.NET 10 Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)
- DevExpress NuGet feed configured (v26.1)
- Git on `PATH`
- GitHub Personal Access Token with SSO authorized for the DevExpress org

## Build

```bash
bash build.sh
```

Output lands in `./release/`.

## Run

```bash
./release/DXCP.WinForms.exe
```

## First Launch

On first run you will be prompted for your GitHub PAT. It is stored securely in Windows Credential Manager and reused on subsequent starts.

To create and authorize a PAT:

1. Go to **GitHub → Settings → Developer settings → Personal access tokens → Tokens (classic)**
2. Click **Generate new token (classic)**
3. Give it a name (e.g. `DXCherryPick`) and select the **`repo`** scope
4. Click **Generate token** and copy the value immediately — GitHub only shows it once
5. **Authorize SSO for the DevExpress organization:**
   - After generating the token, find it in the token list
   - Click **Configure SSO** next to the token
   - Click **Authorize** next to **DevExpress**
   - Complete the SSO sign-in if prompted

> **Why SSO authorization is required:** The DevExpress GitHub organization enforces SAML single sign-on. A PAT without SSO authorization will authenticate successfully against the GitHub API but will receive empty results when querying organization repositories and pull requests. The token must be SSO-authorized before use.
