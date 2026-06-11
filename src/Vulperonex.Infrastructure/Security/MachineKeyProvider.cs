using System.Security.Cryptography;

namespace Vulperonex.Infrastructure.Security;

public sealed class MachineKeyProvider(IFileSystem fileSystem, string? rootPath = null)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private byte[]? _cachedKey;

    public async Task<byte[]> GetKeyAsync(CancellationToken cancellationToken = default)
    {
        // The key is immutable once created, so it is safe to cache for the process
        // lifetime; without this, every ETag computation re-reads machine.key from disk.
        if (_cachedKey is not null)
        {
            return _cachedKey;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_cachedKey is not null)
            {
                return _cachedKey;
            }

            var path = GetKeyPath();
            if (!fileSystem.FileExists(path))
            {
                var key = RandomNumberGenerator.GetBytes(32);
                fileSystem.CreateDirectory(Path.GetDirectoryName(path)!);
                fileSystem.WriteAllBytes(path, key);
                fileSystem.ApplyUserOnlyPermissions(path);
            }

            var bytes = fileSystem.ReadAllBytes(path);
            if (bytes.Length != 32)
            {
                throw new IOException("machine.key must contain exactly 32 bytes.");
            }

            _cachedKey = bytes;
            return bytes;
        }
        finally
        {
            _gate.Release();
        }
    }

    private string GetKeyPath()
    {
        var root = rootPath
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Vulperonex");
        return Path.Combine(root, "machine.key");
    }
}
