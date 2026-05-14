namespace Vulperonex.Infrastructure.Security;

public interface IFileSystem
{
    bool FileExists(string path);

    byte[] ReadAllBytes(string path);

    void WriteAllBytes(string path, byte[] bytes);

    void CreateDirectory(string path);
}
