using FluentAssertions;
using Vulperonex.Application.Members;
using Vulperonex.Domain.Members;
using Xunit;

namespace Vulperonex.Tests.Unit.Application.Members;

public sealed class MemberPortContractTests
{
    [Fact]
    public void Given_MemberRepositoryPort_When_Inspected_Then_ItOnlyExposesWriteMethods()
    {
        var methodNames = typeof(IMemberRepository)
            .GetMethods()
            .Select(method => method.Name)
            .ToArray();

        methodNames.Should().BeEquivalentTo("AddAsync", "UpdateAsync");
    }

    [Fact]
    public void Given_MemberRepositoryPort_When_Used_Then_ItAcceptsDomainMemberRecords()
    {
        var addMethod = typeof(IMemberRepository).GetMethod("AddAsync");
        var updateMethod = typeof(IMemberRepository).GetMethod("UpdateAsync");

        addMethod.Should().NotBeNull();
        updateMethod.Should().NotBeNull();
        addMethod!.GetParameters()[0].ParameterType.Should().Be<MemberRecord>();
        updateMethod!.GetParameters()[0].ParameterType.Should().Be<MemberRecord>();
    }

    [Fact]
    public void Given_MemberQueryServicePort_When_Inspected_Then_ItReturnsReadModels()
    {
        var methods = typeof(IMemberQueryService).GetMethods();

        methods.Should().OnlyContain(method => method.ReturnType == typeof(Task<MemberReadModel?>));
    }

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
