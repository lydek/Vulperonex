using FluentAssertions;
using Vulperonex.Application.Members;
using Xunit;

namespace Vulperonex.Tests.Unit.Application.Members;

public sealed class MemberDtoTests
{
    [Fact]
    public void Given_MemberReadModel_When_Constructed_Then_ItContainsDtoValues()
    {
        var identity = new PlatformIdentityReadModel("twitch", "12345");
        var loyalty = new LoyaltyReadModel(TotalLoyalty: 100, CheckInCount: 3);
        var member = new MemberReadModel("member-1", [identity], loyalty);

        member.MemberId.Should().Be("member-1");
        member.Identities.Should().ContainSingle().Which.Should().Be(identity);
        member.Loyalty.Should().Be(loyalty);
    }
}
