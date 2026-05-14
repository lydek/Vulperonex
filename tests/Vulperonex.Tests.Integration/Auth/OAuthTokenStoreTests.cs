using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vulperonex.Application.Auth;
using Vulperonex.Application.Settings;
using Vulperonex.Infrastructure.Auth;
using Vulperonex.Infrastructure.Security;
using Vulperonex.Infrastructure.Settings;
using Xunit;

namespace Vulperonex.Tests.Integration.Auth;

public sealed class OAuthTokenStoreTests
{
    [Fact]
    public async Task Given_TwitchRefreshToken_When_Stored_Then_TokenRoundTripsAndPersistedValueIsEncrypted()
    {
        await using var fixture = new Infrastructure.SqliteFixture();
        await using var context = await fixture.CreateContextAsync();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        var settings = new SystemSettingsService(context);
        var keyProvider = new MachineKeyProvider(new LocalFileSystem(), Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var store = new OAuthTokenStore(settings, keyProvider);

        await store.StoreRefreshTokenAsync("twitch", "raw-token", TestContext.Current.CancellationToken);
        var token = await store.GetRefreshTokenAsync("twitch", TestContext.Current.CancellationToken);

        token.Should().Be("raw-token");
        var row = await context.SystemSettings.SingleAsync(TestContext.Current.CancellationToken);
        row.Key.Should().Be(SystemSettingKey.OAuthTwitchRefreshToken);
        row.Category.Should().Be("oauth");
        row.Value.Should().NotContain("raw-token");
        row.Value.Should().Contain("v1:");
    }

    [Fact]
    public async Task Given_UnknownPlatform_When_Stored_Then_ArgumentExceptionIsThrown()
    {
        await using var fixture = new Infrastructure.SqliteFixture();
        await using var context = await fixture.CreateContextAsync();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        var store = new OAuthTokenStore(
            new SystemSettingsService(context),
            new MachineKeyProvider(new LocalFileSystem(), Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))));

        var act = async () => await store.StoreRefreshTokenAsync("youtube", "raw-token", TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("Unknown OAuth platform: youtube");
    }

    [Fact]
    public async Task Given_TamperedCiphertext_When_Read_Then_CredentialDecryptionExceptionIsThrown()
    {
        await using var fixture = new Infrastructure.SqliteFixture();
        await using var context = await fixture.CreateContextAsync();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        var settings = new SystemSettingsService(context);
        var keyProvider = new MachineKeyProvider(new LocalFileSystem(), Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var store = new OAuthTokenStore(settings, keyProvider);
        await store.StoreRefreshTokenAsync("twitch", "raw-token", TestContext.Current.CancellationToken);
        var row = await context.SystemSettings.SingleAsync(TestContext.Current.CancellationToken);
        row.Value = row.Value[..^2] + "AA";
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var act = async () => await store.GetRefreshTokenAsync("twitch", TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<CredentialDecryptionException>();
    }

    [Fact]
    public async Task Given_MissingMachineKey_When_Created_Then_KeyHasThirtyTwoBytes()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var provider = new MachineKeyProvider(new LocalFileSystem(), root);

        var key = await provider.GetKeyAsync(TestContext.Current.CancellationToken);

        key.Should().HaveCount(32);
        File.ReadAllBytes(Path.Combine(root, "machine.key")).Should().HaveCount(32);
    }

    [Fact]
    public void Given_EnvelopeCopiedAcrossSettingKeys_When_Decrypted_Then_CredentialDecryptionExceptionIsThrown()
    {
        var key = Enumerable.Range(0, 32).Select(index => (byte)index).ToArray();
        var encrypted = OAuthTokenEnvelope.Encrypt("raw-token", key, SystemSettingKey.OAuthTwitchRefreshToken);

        var act = () => OAuthTokenEnvelope.Decrypt(encrypted, key, "oauth.other_refresh_token");

        act.Should().Throw<CredentialDecryptionException>();
    }
}
