using FluentAssertions;
using Vulperonex.Domain.Members;
using Xunit;

namespace Vulperonex.Tests.Unit.Domain.Members;

public sealed class LoyaltyInfoTests
{
    [Fact]
    public void Given_DefaultLoyaltyInfo_When_Read_Then_CountsStartAtZero()
    {
        var loyalty = LoyaltyInfo.Empty;

        loyalty.TotalLoyalty.Should().Be(0);
        loyalty.CheckInCount.Should().Be(0);
    }
}
