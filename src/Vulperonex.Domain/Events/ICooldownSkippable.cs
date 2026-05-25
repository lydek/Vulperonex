namespace Vulperonex.Domain.Events;

public interface ICooldownSkippable
{
    bool SkipCooldown { get; }
}
