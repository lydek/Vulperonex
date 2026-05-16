using FluentAssertions;
using Vulperonex.Web.Ports;
using Xunit;

namespace Vulperonex.Tests.Unit.Web;

public sealed class PortPairAllocatorTests
{
    [Fact]
    public void Given_FirstPairIsBusy_When_Allocating_Then_NextPairIsSelected()
    {
        var allocator = new PortPairAllocator(new FakeProbe([5001]));

        allocator.TryAllocate().Should().Be(new PortPair(5002, 5003));
    }

    [Fact]
    public void Given_AllPairsAreBusy_When_Allocating_Then_NullIsReturned()
    {
        var allocator = new PortPairAllocator(new FakeProbe([5000, 5002, 5004, 5006, 5008]));

        allocator.TryAllocate().Should().BeNull();
    }

    private sealed class FakeProbe(IReadOnlyCollection<int> busyPorts) : IPortAvailabilityProbe
    {
        public bool IsAvailable(int port) => !busyPorts.Contains(port);
    }
}
