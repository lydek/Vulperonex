using FluentAssertions;
using Vulperonex.Domain.Members;
using Xunit;

namespace Vulperonex.Tests.Unit.Domain.Members;

public sealed class MemberRecordTests
{
    [Fact]
    public void Given_ValidPlatformIdentity_When_CreatingMemberRecord_Then_MemberIdAndIdentityAreExposed()
    {
        var identity = PlatformIdentity.Create("twitch", "12345");
        var member = MemberRecord.Create(identity);

        member.MemberId.Should().MatchRegex("^[0-9A-HJKMNP-TV-Z]{26}$");
        member.Identities.Should().ContainSingle().Which.Should().Be(identity);
    }

    [Fact]
    public void Given_AdditionalIdentity_When_Added_Then_MemberRecordContainsBothIdentities()
    {
        var member = MemberRecord.Create(PlatformIdentity.Create("twitch", "12345"));
        var additionalIdentity = PlatformIdentity.Create("youtube", "abcde");

        member.AddIdentity(additionalIdentity);

        member.Identities.Should().BeEquivalentTo([
            PlatformIdentity.Create("twitch", "12345"),
            additionalIdentity,
        ]);
    }

    [Fact]
    public void Given_DuplicateIdentity_When_Added_Then_MemberRecordKeepsSingleIdentity()
    {
        var identity = PlatformIdentity.Create("twitch", "12345");
        var member = MemberRecord.Create(identity);

        member.AddIdentity(PlatformIdentity.Create("twitch", "12345"));

        member.Identities.Should().ContainSingle().Which.Should().Be(identity);
    }
}
