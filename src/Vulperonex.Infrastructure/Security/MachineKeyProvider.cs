using System.Security.Cryptography;

namespace Vulperonex.Infrastructure.Security;

public sealed class MachineKeyProvider(IFileSystem fileSystem, string? rootPath = null)
{
    public async Task<byte[]> GetKeyAsync(CancellationToken cancellationToken = default)
    {
        var path = GetKeyPath();
        if (!fileSystem.FileExists(path))
        {
            var key = RandomNumberGenerator.GetBytes(32);
            fileSystem.CreateDirectory(Path.GetDirectoryName(path)!);
            fileSystem.WriteAllBytes(path, key);
        }

        var bytes = fileSystem.ReadAllBytes(path);
        if (bytes.Length != 32)
        {
            throw new IOException("machine.key must contain exactly 32 bytes.");
        }

        await Task.CompletedTask.WaitAsync(cancellationToken);
        return bytes;
    }

    private string GetKeyPath()
    {
        var root = rootPath
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Vulperonex");
        return Path.Combine(root, "machine.key");
    }
}
