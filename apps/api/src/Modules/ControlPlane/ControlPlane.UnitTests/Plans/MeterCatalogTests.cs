using ControlPlane.Domain.Plans;

namespace ControlPlane.UnitTests.Plans;

public sealed class MeterCatalogTests
{
    // The template ships no meter (the maintainer's decision of 2026-11-14), and an empty catalog is sound.
    [Fact]
    public void CheckCatalog_TheTemplate_DefinesNoMeterAndHasNoMistake() =>
        (MeterCatalog.Template.Meters.Count, MeterCatalog.Template.Mistakes().Count).ShouldBe((0, 0));

    // X5: the code's catalog holds the template's meters and the product's, and a product's meter has no mistake either.
    [Fact]
    public void CheckCatalog_TheCode_HoldsTheTemplatesAndTheProductsMetersWithoutAMistake() =>
        (MeterCatalog.Code.Meters.Count, MeterCatalog.Code.Mistakes().Count).ShouldBe((MeterCatalog.Template.Meters.Count + ProductMeters.All.Count, 0));

    [Fact]
    public void CheckCatalog_AKeyDefinedTwice_IsAMistake() =>
        new MeterCatalog([Meter("ai.requests"), Meter("ai.requests")]).Mistakes().ShouldBe(["ai.requests is defined twice."]);

    [Theory]
    [InlineData("requests")]
    [InlineData("AI.Requests")]
    public void CheckCatalog_AKeyThatIsNotLowercaseAndDotted_IsAMistake(string key) =>
        new MeterCatalog([Meter(key)]).Mistakes().ShouldBe([$"{key} is not a lowercase, dotted key."]);

    [Fact]
    public void CheckCatalog_AMeterWithoutADescription_IsAMistake() =>
        new MeterCatalog([new MeterDefinition("ai.requests", " ")]).Mistakes().ShouldBe(["ai.requests has no description."]);

    private static MeterDefinition Meter(string key) => new(key, "What the meter counts.");
}
