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
        var options = new PortAllocationOptions();
        var busyApiPorts = Enumerable
            .Range(0, ((options.LastApiPort - options.FirstApiPort) / options.PortStep) + 1)
            .Select(index => options.FirstApiPort + (index * options.PortStep))
            .ToArray();
        var allocator = new PortPairAllocator(new FakeProbe(busyApiPorts), options);

        allocator.TryAllocate().Should().BeNull();
    }

    [Fact]
    public void Given_CustomPortRange_When_Allocating_Then_OptionsControlPairs()
    {
        var allocator = new PortPairAllocator(
            new FakeProbe([7000]),
            new PortAllocationOptions(7000, 7004));

        allocator.TryAllocate().Should().Be(new PortPair(7002, 7003));
    }

    private sealed class FakeProbe(IReadOnlyCollection<int> busyPorts) : IPortAvailabilityProbe
    {
        public bool IsAvailable(int port) => !busyPorts.Contains(port);
    }
}
