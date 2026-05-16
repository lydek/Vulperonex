namespace Vulperonex.Web.Ports;

public sealed class PortExhaustedException() : InvalidOperationException(
    "No loopback API/overlay port pair is available from 5000/5001 through 5008/5009.");
