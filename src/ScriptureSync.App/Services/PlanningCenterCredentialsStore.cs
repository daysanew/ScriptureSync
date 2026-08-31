using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using ScriptureSync.Core.Configuration;
using ScriptureSync.Core.Logging;

namespace ScriptureSync.App.Services;

public sealed record PlanningCenterCredentials(string ApplicationId, string Secret)
{
    public bool IsComplete => !string.IsNullOrWhiteSpace(ApplicationId) && !string.IsNullOrWhiteSpace(Secret);
}

public sealed class PlanningCenterCredentialsStore
{
    private const string CredentialTarget = "ScriptureSync:PlanningCenter";
    private static readonly byte[] LegacyEntropy = "ScriptureSync.PlanningCenter.v1"u8.ToArray();
    private readonly LocalAppPaths _paths;
    private readonly IAppLogger _logger;

    public PlanningCenterCredentialsStore(LocalAppPaths paths, IAppLogger logger)
    {
        _paths = paths;
        _logger = logger;
    }

    public PlanningCenterCredentials Load()
    {
        try
        {
            var stored = ReadCredentialManager();
            return stored.IsComplete ? stored : MigrateLegacyCredentials();
        }
        catch (Exception exception)
        {
            _logger.Error("Planning Center credentials could not be loaded.", exception);
            return new(string.Empty, string.Empty);
        }
    }

    public void Save(PlanningCenterCredentials credentials)
    {
        if (!credentials.IsComplete)
            throw new ArgumentException("Both the Planning Center Application ID and Secret are required.");

        var secretBytes = System.Text.Encoding.Unicode.GetBytes(credentials.Secret);
        var secretPointer = Marshal.AllocCoTaskMem(secretBytes.Length);
        try
        {
            Marshal.Copy(secretBytes, 0, secretPointer, secretBytes.Length);
            var credential = new NativeCredential
            {
                Type = CredentialType.Generic,
                TargetName = CredentialTarget,
                CredentialBlobSize = (uint)secretBytes.Length,
                CredentialBlob = secretPointer,
                Persist = CredentialPersist.LocalMachine,
                UserName = credentials.ApplicationId,
                Comment = "Planning Center Personal Access Token for ScriptureSync"
            };
            if (!CredWrite(ref credential, 0)) throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        catch (Exception exception)
        {
            _logger.Error("Planning Center credentials could not be saved to Windows Credential Manager.", exception);
            throw;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secretBytes);
            for (var index = 0; index < secretBytes.Length; index++) Marshal.WriteByte(secretPointer, index, 0);
            Marshal.FreeCoTaskMem(secretPointer);
        }
    }

    public void Delete()
    {
        if (!CredDelete(CredentialTarget, CredentialType.Generic, 0))
        {
            const int ErrorNotFound = 1168;
            var error = Marshal.GetLastWin32Error();
            if (error != ErrorNotFound) throw new Win32Exception(error);
        }
    }

    private static PlanningCenterCredentials ReadCredentialManager()
    {
        if (!CredRead(CredentialTarget, CredentialType.Generic, 0, out var pointer))
        {
            const int ErrorNotFound = 1168;
            var error = Marshal.GetLastWin32Error();
            if (error == ErrorNotFound) return new(string.Empty, string.Empty);
            throw new Win32Exception(error);
        }

        try
        {
            var credential = Marshal.PtrToStructure<NativeCredential>(pointer);
            var secret = credential.CredentialBlob == IntPtr.Zero
                ? string.Empty
                : Marshal.PtrToStringUni(credential.CredentialBlob, (int)credential.CredentialBlobSize / 2)
                  ?? string.Empty;
            return new(credential.UserName ?? string.Empty, secret);
        }
        finally
        {
            CredFree(pointer);
        }
    }

    private PlanningCenterCredentials MigrateLegacyCredentials()
    {
        if (!File.Exists(_paths.PlanningCenterCredentialsFile)) return new(string.Empty, string.Empty);

        var protectedBytes = File.ReadAllBytes(_paths.PlanningCenterCredentialsFile);
        var bytes = ProtectedData.Unprotect(protectedBytes, LegacyEntropy, DataProtectionScope.CurrentUser);
        var credentials = JsonSerializer.Deserialize<PlanningCenterCredentials>(bytes)
                          ?? new(string.Empty, string.Empty);
        CryptographicOperations.ZeroMemory(bytes);
        if (!credentials.IsComplete) return credentials;

        Save(credentials);
        File.Delete(_paths.PlanningCenterCredentialsFile);
        _logger.Info("Planning Center credentials were migrated to Windows Credential Manager.");
        return credentials;
    }

    private enum CredentialType : uint { Generic = 1 }
    private enum CredentialPersist : uint { LocalMachine = 2 }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential
    {
        public uint Flags;
        public CredentialType Type;
        public string? TargetName;
        public string? Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public CredentialPersist Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias;
        public string? UserName;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredWrite(ref NativeCredential credential, uint flags);

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredRead(string target, CredentialType type, uint flags, out IntPtr credential);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDelete(string target, CredentialType type, uint flags);

    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr buffer);
}
