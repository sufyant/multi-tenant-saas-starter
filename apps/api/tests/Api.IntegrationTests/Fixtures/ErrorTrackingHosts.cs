namespace Api.IntegrationTests.Fixtures;

// The error tracker's SDK keeps one hub for the whole process, and each host with a DSN replaces it. The test classes whose hosts have a
// DSN are in this collection, so xUnit runs them one after the other and each test reads only the events of its own host. A host without
// a DSN never touches the hub, so the other tests run beside them.
[CollectionDefinition]
public sealed class ErrorTrackingHosts;
