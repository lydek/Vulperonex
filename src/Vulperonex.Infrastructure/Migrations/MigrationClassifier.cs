using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Vulperonex.Infrastructure.Migrations;

public static class MigrationClassifier
{
    private static readonly string[] DestructiveSqlTokens = ["DROP", "DELETE", "TRUNCATE", "RENAME"];

    public static MigrationRisk Classify(IReadOnlyList<MigrationOperation> operations)
    {
        var highestRisk = MigrationRisk.Safe;

        foreach (var operation in operations)
        {
            if (operation is SqlOperation sqlOperation)
            {
                highestRisk = Max(highestRisk, ClassifySql(sqlOperation.Sql));
            }
        }

        return highestRisk;
    }

    private static MigrationRisk ClassifySql(string sql)
    {
        var normalized = sql.ToUpperInvariant();

        if (DestructiveSqlTokens.Any(token => normalized.Contains(token, StringComparison.Ordinal)))
        {
            return MigrationRisk.Destructive;
        }

        if (normalized.Contains("ALTER", StringComparison.Ordinal))
        {
            return MigrationRisk.ReviewRequired;
        }

        return MigrationRisk.Safe;
    }

    private static MigrationRisk Max(MigrationRisk left, MigrationRisk right)
    {
        return (MigrationRisk)Math.Max((int)left, (int)right);
    }
}
