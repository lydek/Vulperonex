namespace Vulperonex.Domain;

public sealed record StreamUser(string Platform, string UserId, string DisplayName, StreamRole Roles = StreamRole.None);
