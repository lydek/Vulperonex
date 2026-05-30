using Xunit;

// Disable parallel test execution in the integration test project.
// Integration tests launch concurrent Web Hosts sharing EF Core SQLite In-Memory
// connections, which causes flaky resource locks and "active statements" conflicts.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
