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

To create a PAT:
1. Go to **GitHub → Settings → Developer settings → Personal access tokens**
2. Generate a token with the `repo` scope
3. Click **Configure SSO** and authorize it for the **DevExpress** organization
