using FluentAssertions;
using Vulperonex.Infrastructure.Security;
using Xunit;

namespace Vulperonex.Tests.Unit.Infrastructure.Security;

public sealed class MachineKeyProviderTests
{
    [Fact]
    public async Task Given_MissingMachineKey_When_Created_Then_UserOnlyPermissionsAreApplied()
    {
        var fileSystem = new FakeFileSystem();
        var provider = new MachineKeyProvider(fileSystem, "app-data");

        await provider.GetKeyAsync(TestContext.Current.CancellationToken);

        fileSystem.SecuredPaths.Should().Contain(@"app-data\machine.key");
    }

    [Fact]
    public async Task Given_PermissionFailure_When_KeyIsCreated_Then_IOExceptionIsThrown()
    {
        var fileSystem = new FakeFileSystem { ThrowOnSecure = true };
        var provider = new MachineKeyProvider(fileSystem, "app-data");

        var act = async () => await provider.GetKeyAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<IOException>().WithMessage("permission failure");
    }

    private sealed class FakeFileSystem : IFileSystem
    {
        private readonly Dictionary<string, byte[]> _files = [];

        public List<string> SecuredPaths { get; } = [];

        public bool ThrowOnSecure { get; init; }

        public bool FileExists(string path) => _files.ContainsKey(path);

        public byte[] ReadAllBytes(string path) => _files[path];

        public void WriteAllBytes(string path, byte[] bytes) => _files[path] = bytes;

        public void CreateDirectory(string path)
        {
        }

        public void ApplyUserOnlyPermissions(string path)
        {
            if (ThrowOnSecure)
            {
                throw new IOException("permission failure");
            }

            SecuredPaths.Add(path);
        }
    }
}
