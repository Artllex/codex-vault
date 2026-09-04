using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security;

namespace WindowsSecretManager.Core;

public sealed class WindowsCredentialStore : ISecretStore
{
    private const string ManagedComment = "Managed by Codex Vault";
    private const uint CredTypeGeneric = 1;
    private const uint CredPersistLocalMachine = 2;
    private const int ErrorNotFound = 1168;

    public IReadOnlyList<string> ListNames()
    {
        if (!CredEnumerate(null, 0, out var count, out var array))
        {
            var error = Marshal.GetLastWin32Error();
            if (error == ErrorNotFound) return Array.Empty<string>();
            throw new Win32Exception(error, "Nie udało się odczytać listy poświadczeń Windows.");
        }
        try
        {
            var names = new List<string>((int)count);
            for (var i = 0; i < count; i++)
            {
                var item = Marshal.ReadIntPtr(array, i * IntPtr.Size);
                var credential = Marshal.PtrToStructure<CREDENTIAL>(item);
                if (credential.TargetName is not null &&
                    (credential.Comment == ManagedComment ||
                     credential.TargetName.StartsWith(SecretNames.Prefix, StringComparison.Ordinal) ||
                     credential.TargetName.StartsWith(SecretNames.SharedPrefix, StringComparison.Ordinal)))
                    names.Add(credential.TargetName);
            }
            return names;
        }
        finally { CredFree(array); }
    }

    public bool Exists(string name)
    {
        if (CredRead(name, CredTypeGeneric, 0, out var ptr)) { CredFree(ptr); return true; }
        var error = Marshal.GetLastWin32Error();
        if (error == ErrorNotFound) return false;
        throw new Win32Exception(error, "Nie udało się sprawdzić poświadczenia Windows.");
    }

    public void Save(string name, SecureString value)
    {
        var secret = Marshal.SecureStringToCoTaskMemUnicode(value);
        try
        {
            var credential = new CREDENTIAL
            {
                Type = CredTypeGeneric, TargetName = name, CredentialBlobSize = (uint)(value.Length * 2),
                CredentialBlob = secret, Persist = CredPersistLocalMachine, UserName = Environment.UserName,
                Comment = ManagedComment
            };
            if (!CredWrite(ref credential, 0)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Nie udało się zapisać poświadczenia Windows.");
        }
        finally { Marshal.ZeroFreeCoTaskMemUnicode(secret); }
    }

    public SecureString Read(string name)
    {
        if (!CredRead(name, CredTypeGeneric, 0, out var ptr)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Nie udało się odczytać poświadczenia Windows.");
        try
        {
            var credential = Marshal.PtrToStructure<CREDENTIAL>(ptr);
            var result = new SecureString();
            var chars = checked((int)credential.CredentialBlobSize / 2);
            for (var i = 0; i < chars; i++) result.AppendChar((char)Marshal.ReadInt16(credential.CredentialBlob, i * 2));
            result.MakeReadOnly();
            return result;
        }
        finally { CredFree(ptr); }
    }

    public bool Delete(string name)
    {
        if (CredDelete(name, CredTypeGeneric, 0)) return true;
        var error = Marshal.GetLastWin32Error();
        if (error == ErrorNotFound) return false;
        throw new Win32Exception(error, "Nie udało się usunąć poświadczenia Windows.");
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CREDENTIAL
    {
        public uint Flags, Type;
        public string? TargetName, Comment;
        public long LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist, AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias, UserName;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredEnumerateW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredEnumerate(string? filter, uint flags, out uint count, out IntPtr credentials);
    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(string target, uint type, uint flags, out IntPtr credential);
    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWrite([In] ref CREDENTIAL credential, uint flags);
    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDelete(string target, uint type, uint flags);
    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr buffer);
}
