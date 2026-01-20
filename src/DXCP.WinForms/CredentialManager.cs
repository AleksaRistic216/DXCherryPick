using System.Runtime.InteropServices;
using System.Text;

namespace DXCP.WinForms;

/// <summary>
/// Provides secure credential storage using Windows Credential Manager.
/// Credentials are encrypted using DPAPI and tied to the current Windows user.
/// </summary>
public static class CredentialManager
{
    private const string CredentialName = "DXCherryPick:GitHubPAT";

    public static void SaveToken(string token)
    {
        var byteArray = Encoding.UTF8.GetBytes(token);

        var credential = new CREDENTIAL
        {
            Type = CRED_TYPE_GENERIC,
            TargetName = CredentialName,
            CredentialBlobSize = byteArray.Length,
            CredentialBlob = Marshal.AllocHGlobal(byteArray.Length),
            Persist = CRED_PERSIST_LOCAL_MACHINE,
            UserName = Environment.UserName
        };

        try
        {
            Marshal.Copy(byteArray, 0, credential.CredentialBlob, byteArray.Length);

            if (!CredWrite(ref credential, 0))
            {
                throw new InvalidOperationException(
                    $"Failed to save credential. Error code: {Marshal.GetLastWin32Error()}");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(credential.CredentialBlob);
        }
    }

    public static string? GetToken()
    {
        if (!CredRead(CredentialName, CRED_TYPE_GENERIC, 0, out var credentialPtr))
        {
            return null;
        }

        try
        {
            var credential = Marshal.PtrToStructure<CREDENTIAL>(credentialPtr);
            if (credential.CredentialBlob == IntPtr.Zero || credential.CredentialBlobSize == 0)
            {
                return null;
            }

            var byteArray = new byte[credential.CredentialBlobSize];
            Marshal.Copy(credential.CredentialBlob, byteArray, 0, credential.CredentialBlobSize);
            return Encoding.UTF8.GetString(byteArray);
        }
        finally
        {
            CredFree(credentialPtr);
        }
    }

    public static void DeleteToken()
    {
        CredDelete(CredentialName, CRED_TYPE_GENERIC, 0);
    }

    #region Windows Credential Manager P/Invoke

    private const int CRED_TYPE_GENERIC = 1;
    private const int CRED_PERSIST_LOCAL_MACHINE = 2;

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredWrite(ref CREDENTIAL credential, int flags);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredRead(string target, int type, int flags, out IntPtr credential);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredDelete(string target, int type, int flags);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern void CredFree(IntPtr credential);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CREDENTIAL
    {
        public int Flags;
        public int Type;
        public string TargetName;
        public string Comment;
        public long LastWritten;
        public int CredentialBlobSize;
        public IntPtr CredentialBlob;
        public int Persist;
        public int AttributeCount;
        public IntPtr Attributes;
        public string TargetAlias;
        public string UserName;
    }

    #endregion
}
