namespace Api.ArchitectureTests.Fixtures;

// A table of a cached entity type, and the name of its tag's constant in the module's CacheTags (BulkWrites).
internal sealed record CachedTable(string Name, Type Entity, string KindConstant);
