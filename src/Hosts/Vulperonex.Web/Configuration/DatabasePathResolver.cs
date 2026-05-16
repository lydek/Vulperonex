namespace Vulperonex.Web.Configuration;

public sealed class DatabasePathResolver(IConfiguration configuration) : IDatabasePathResolver
{
    public string Resolve()
    {
        return configuration["Database:Path"] ?? "vulperonex.db";
    }
}
