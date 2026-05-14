namespace Vulperonex.Infrastructure.Security;

public sealed class LocalFileSystem : IFileSystem
{
    public bool FileExists(string path) => File.Exists(path);

    public byte[] ReadAllBytes(string path) => File.ReadAllBytes(path);

    public void WriteAllBytes(string path, byte[] bytes) => File.WriteAllBytes(path, bytes);

    public void CreateDirectory(string path) => Directory.CreateDirectory(path);
}
