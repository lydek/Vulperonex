using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Vulperonex.Infrastructure.Security;

public sealed class LocalFileSystem : IFileSystem
{
    public bool FileExists(string path) => File.Exists(path);

    public byte[] ReadAllBytes(string path) => File.ReadAllBytes(path);

    public void WriteAllBytes(string path, byte[] bytes) => File.WriteAllBytes(path, bytes);

    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    public void ApplyUserOnlyPermissions(string path)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                ApplyWindowsUserOnlyPermissions(path);
                return;
            }

            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (IOException)
        {
            throw;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or SystemException)
        {
            throw new IOException($"Failed to secure machine.key permissions: {path}", exception);
        }
    }

    [SupportedOSPlatform("windows")]
    private static void ApplyWindowsUserOnlyPermissions(string path)
    {
        var currentUser = WindowsIdentity.GetCurrent().User
            ?? throw new IOException("Unable to determine the current Windows user.");

        var security = new FileSecurity();
        security.SetOwner(currentUser);
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.AddAccessRule(new FileSystemAccessRule(
            currentUser,
            FileSystemRights.FullControl,
            AccessControlType.Allow));

        var file = new FileInfo(path);
        file.SetAccessControl(security);
    }
}
