namespace Vulperonex.Application.Modules;

public sealed class DependencyMissingException(string message) : InvalidOperationException(message);
