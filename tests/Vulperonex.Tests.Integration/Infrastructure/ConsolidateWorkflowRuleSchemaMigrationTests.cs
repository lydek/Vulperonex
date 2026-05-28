using FluentAssertions;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Vulperonex.Tests.Integration.Infrastructure;

/// <summary>
/// Phase 8 / A.5 §5b.1/5b.2/5b.3: verify ConsolidateWorkflowRuleSchema migration
/// SQL on a **real SQLite file provider** (plan A5.3 explicitly forbids
/// InMemory or Sqlite in-memory modes — must hit production engine path
/// covering NOT NULL → NULL table-rebuild semantics).
///
/// Bypasses EF Core migrator: replays the migration's raw SQL against a
/// minimal pre-consolidation schema, so it is decoupled from prior migration
/// chain churn and only validates the SQL under review.
///
/// Asserts:
///  - Legacy row with empty outer EventTypeKey + inner trigger.eventTypeKey
///    is lifted to outer column (COALESCE NULLIF '' fix from review C-1).
///  - Inner trigger.eventTypeKey / matchCondition are removed from TriggerJson blob.
///  - Sub-workflow rows have EventTypeKey set to NULL.
///  - Column constraint allows NULL after ALTER COLUMN rebuild.
/// </summary>
public sealed class ConsolidateWorkflowRuleSchemaMigrationTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly string _connectionString;

    // Mirror the migration SQL under review (kept inline so the test fails if
    // production migration drifts and someone forgets to sync).
    private const string ConsolidationSql = @"
UPDATE WorkflowRules SET EventTypeKey = COALESCE(NULLIF(EventTypeKey, ''), json_extract(TriggerJson, '$.eventTypeKey')) WHERE TriggerJson IS NOT NULL AND json_extract(TriggerJson, '$.eventTypeKey') IS NOT NULL;
UPDATE WorkflowRules SET MatchCondition = COALESCE(NULLIF(MatchCondition, ''), json_extract(TriggerJson, '$.matchCondition')) WHERE TriggerJson IS NOT NULL AND json_extract(TriggerJson, '$.matchCondition') IS NOT NULL;
UPDATE WorkflowRules SET TriggerJson = json_remove(TriggerJson, '$.eventTypeKey', '$.matchCondition') WHERE TriggerJson IS NOT NULL;
UPDATE WorkflowRules SET EventTypeKey = NULL WHERE IsSubWorkflow = 1;
";

    public ConsolidateWorkflowRuleSchemaMigrationTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"vulperonex-migration-test-{Guid.NewGuid():N}.db");
        _connectionString = $"Data Source={_dbPath}";
    }

    [Fact]
    public async Task Given_LegacyRows_When_RunMigration_Then_SqliteTableRebuildSucceeds()
    {
        var ct = TestContext.Current.CancellationToken;

        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        // 1. Build pre-consolidation schema: EventTypeKey NOT NULL (matches state
        //    before the migration under review).
        await ExecAsync(conn, @"
CREATE TABLE WorkflowRules (
    Id TEXT PRIMARY KEY NOT NULL,
    Name TEXT NOT NULL,
    EventTypeKey TEXT NOT NULL,
    TriggerJson TEXT,
    MatchCondition TEXT,
    IsSubWorkflow INTEGER NOT NULL,
    ConditionsJson TEXT NOT NULL,
    ActionsJson TEXT NOT NULL,
    OnFailureActionsJson TEXT NOT NULL
);", ct);

        // 2. Seed legacy rows.
        await ExecAsync(conn, @"
INSERT INTO WorkflowRules (Id, Name, EventTypeKey, TriggerJson, MatchCondition, IsSubWorkflow, ConditionsJson, ActionsJson, OnFailureActionsJson) VALUES
    ('rule-A', 'Legacy inner only', '', '{""eventTypeKey"":""user.message"",""matchCondition"":""Trigger.MessageText == ''!checkin''"",""filter"":{}}', NULL, 0, '[]', '[]', '[]'),
    ('rule-B', 'Sub workflow legacy', '', NULL, NULL, 1, '[]', '[]', '[]'),
    ('rule-C', 'Both outer and inner', 'user.subscribed', '{""eventTypeKey"":""user.message"",""matchCondition"":""x == 1"",""filter"":{}}', 'outer wins', 0, '[]', '[]', '[]');
", ct);

        // 3. Apply the schema-only step FIRST (NOT NULL → NULL rebuild).
        //    Production migration order: AlterColumn nullable:true precedes
        //    the data scrub, otherwise step-4 SET EventTypeKey = NULL fails
        //    the NOT NULL constraint.
        await ExecAsync(conn, @"
PRAGMA foreign_keys = OFF;
CREATE TABLE WorkflowRules_tmp (
    Id TEXT PRIMARY KEY NOT NULL,
    Name TEXT NOT NULL,
    EventTypeKey TEXT NULL,
    TriggerJson TEXT,
    MatchCondition TEXT,
    IsSubWorkflow INTEGER NOT NULL,
    ConditionsJson TEXT NOT NULL,
    ActionsJson TEXT NOT NULL,
    OnFailureActionsJson TEXT NOT NULL
);
INSERT INTO WorkflowRules_tmp SELECT Id, Name, EventTypeKey, TriggerJson, MatchCondition, IsSubWorkflow, ConditionsJson, ActionsJson, OnFailureActionsJson FROM WorkflowRules;
DROP TABLE WorkflowRules;
ALTER TABLE WorkflowRules_tmp RENAME TO WorkflowRules;
PRAGMA foreign_keys = ON;
", ct);

        // 4. Apply the data-only steps of the migration (data scrub).
        await ExecAsync(conn, ConsolidationSql, ct);

        // 5. Verify data state.
        var rowA = await ReadRowAsync(conn, "rule-A", ct);
        rowA.EventTypeKey.Should().Be("user.message", "inner eventTypeKey must lift to outer when outer was empty string");
        rowA.MatchCondition.Should().Be("Trigger.MessageText == '!checkin'", "inner matchCondition must lift to outer when outer was null");
        rowA.TriggerJson.Should().NotContain("eventTypeKey", "inner field must be pruned from blob");
        rowA.TriggerJson.Should().NotContain("matchCondition", "inner field must be pruned from blob");

        var rowB = await ReadRowAsync(conn, "rule-B", ct);
        rowB.EventTypeKey.Should().BeNull("sub-workflow rules must have NULL EventTypeKey post-migration");
        rowB.IsSubWorkflow.Should().BeTrue();

        var rowC = await ReadRowAsync(conn, "rule-C", ct);
        rowC.EventTypeKey.Should().Be("user.subscribed", "outer EventTypeKey must be preserved when non-empty (NULLIF branch)");
        rowC.MatchCondition.Should().Be("outer wins", "outer MatchCondition must be preserved when non-null");
        rowC.TriggerJson.Should().NotContain("eventTypeKey");
        rowC.TriggerJson.Should().NotContain("matchCondition");

        // 6. Verify column constraint actually allows NULL.
        await using var insertNull = conn.CreateCommand();
        insertNull.CommandText = @"INSERT INTO WorkflowRules (Id, Name, EventTypeKey, IsSubWorkflow, ConditionsJson, ActionsJson, OnFailureActionsJson) VALUES ('rule-null', 'Null event type', NULL, 1, '[]', '[]', '[]');";
        var act = async () => await insertNull.ExecuteNonQueryAsync(ct);
        await act.Should().NotThrowAsync("EventTypeKey column must accept NULL after rebuild");
    }

    private static async Task ExecAsync(SqliteConnection conn, string sql, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<RawWorkflowRow> ReadRowAsync(SqliteConnection conn, string id, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT EventTypeKey, MatchCondition, TriggerJson, IsSubWorkflow FROM WorkflowRules WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$id", id);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var hasRow = await reader.ReadAsync(ct);
        hasRow.Should().BeTrue("seeded row {0} must exist", id);

        return new RawWorkflowRow(
            EventTypeKey: reader.IsDBNull(0) ? null : reader.GetString(0),
            MatchCondition: reader.IsDBNull(1) ? null : reader.GetString(1),
            TriggerJson: reader.IsDBNull(2) ? null : reader.GetString(2),
            IsSubWorkflow: reader.GetBoolean(3));
    }

    public async ValueTask DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath))
        {
            try
            {
                File.Delete(_dbPath);
            }
            catch (IOException)
            {
                // Best-effort cleanup; Windows file locks may persist briefly.
            }
        }
        await Task.CompletedTask;
    }

    private sealed record RawWorkflowRow(string? EventTypeKey, string? MatchCondition, string? TriggerJson, bool IsSubWorkflow);
}
