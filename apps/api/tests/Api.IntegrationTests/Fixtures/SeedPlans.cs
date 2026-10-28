namespace Api.IntegrationTests.Fixtures;

// The ids of the plans that ControlPlane's migration AddPlansAndEntitlements seeds. Free is the default plan; Pro holds what every tenant
// had before the plans. A test never edits them: the tests share one database, so a test that edits a plan creates its own.
internal static class SeedPlans
{
    public static readonly Guid Free = new("0199c000-0000-7000-8000-00000000f4ee");

    public static readonly Guid Pro = new("0199c000-0000-7000-8000-000000000940");
}
