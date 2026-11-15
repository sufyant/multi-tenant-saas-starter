namespace Api.IntegrationTests.Fixtures;

// The ids of the seeded plans. AddPlansAndEntitlements seeded Free, the default plan, and Pro, which held what every tenant had before
// the plans; step 40's AddPlanPricesAndMeters renamed Pro to Business (50 seats) and added Team. A test never edits them: the tests share one database, so a test that edits a plan creates its own.
internal static class SeedPlans
{
    public static readonly Guid Free = new("0199c000-0000-7000-8000-00000000f4ee");

    public static readonly Guid Business = new("0199c000-0000-7000-8000-000000000940");

    public static readonly Guid Team = new("0199c000-0000-7000-8000-0000000007ea");
}
