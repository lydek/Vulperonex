namespace Vulperonex.Web.Configuration;

public sealed class DatabasePathResolver(IConfiguration configuration) : IDatabasePathResolver
{
    public string Resolve()
    {
        return configuration["Database:Path"] ?? Path.Combine(AppContext.BaseDirectory, "vulperonex.db");
    }
}
