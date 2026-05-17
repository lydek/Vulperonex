namespace Vulperonex.Web.Ports;

public sealed record PortAllocationOptions(int FirstApiPort = 5000, int LastApiPort = 5008, int PortStep = 2)
{
    public string DescribeRange()
    {
        return $"{FirstApiPort}/{FirstApiPort + 1} through {LastApiPort}/{LastApiPort + 1}";
    }
}
