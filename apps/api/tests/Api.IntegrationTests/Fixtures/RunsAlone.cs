namespace Api.IntegrationTests.Fixtures;

// A test class in this collection runs after every other test of the assembly, and alone: xUnit runs a collection that disables
// parallelization after the parallel ones.
[CollectionDefinition(DisableParallelization = true)]
public sealed class RunsAlone;
