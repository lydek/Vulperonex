namespace Vulperonex.Domain;

[Flags]
public enum StreamRole
{
    None = 0,
    Subscriber = 1,
    Moderator = 2,
    Vip = 4,
    Follower = 8,
    Broadcaster = 16,
}
