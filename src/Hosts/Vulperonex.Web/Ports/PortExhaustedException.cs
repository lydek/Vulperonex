namespace Vulperonex.Web.Ports;

public sealed class PortExhaustedException(PortAllocationOptions? options = null) : InvalidOperationException(
    $"No loopback API/overlay port pair is available from {(options ?? new PortAllocationOptions()).DescribeRange()}.");
