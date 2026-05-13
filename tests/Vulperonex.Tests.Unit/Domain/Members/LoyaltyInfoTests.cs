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

    [Theory]
    [InlineData(-1, 0, "totalLoyalty")]
    [InlineData(0, -1, "checkInCount")]
    public void Given_NegativeLoyaltyValue_When_Constructed_Then_ArgumentOutOfRangeExceptionIsThrown(
        int totalLoyalty,
        int checkInCount,
        string parameterName)
    {
        var act = () => new LoyaltyInfo(totalLoyalty, checkInCount);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName(parameterName);
    }
}
