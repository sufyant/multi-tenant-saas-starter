using ControlPlane.Domain.FeatureFlags;

namespace ControlPlane.UnitTests.FeatureFlags;

public sealed class FeatureFlagCatalogTests
{
    [Fact]
    public void CheckCatalog_TheTemplate_HasNoMistake() => FeatureFlagCatalog.Template.Mistakes().ShouldBeEmpty();

    [Fact]
    public void CheckCatalog_TwoFlagsWithOneKey_IsAMistake() =>
        new FeatureFlagCatalog([Release("roles.bulk_edit"), Release("roles.bulk_edit")], []).Mistakes().ShouldBe(["roles.bulk_edit is defined twice."]);

    [Fact]
    public void CheckCatalog_AFlagWithARetiredKey_IsAMistake() =>
        new FeatureFlagCatalog([Release("roles.bulk_edit")], ["roles.bulk_edit"]).Mistakes()
            .ShouldBe(["roles.bulk_edit is retired; a removed flag's name is never used again."]);

    [Fact]
    public void CheckCatalog_AKeyRetiredTwice_IsAMistake() =>
        new FeatureFlagCatalog([], ["roles.old", "roles.old"]).Mistakes().ShouldBe(["roles.old is retired twice."]);

    [Theory]
    [InlineData("Roles.BulkEdit")]
    [InlineData("bulk_edit")]
    [InlineData("roles..bulk")]
    [InlineData("roles.bulk-edit")]
    public void CheckCatalog_AKeyThatIsNotLowercaseAndDotted_IsAMistake(string key) =>
        new FeatureFlagCatalog([Release(key)], []).Mistakes().ShouldBe([$"{key} is not a lowercase, dotted key."]);

    [Fact]
    public void CheckCatalog_AFlagWithoutADescription_IsAMistake() =>
        new FeatureFlagCatalog([new FeatureFlagDefinition("roles.bulk_edit", FeatureFlagType.Release, " ")], []).Mistakes()
            .ShouldBe(["roles.bulk_edit has no description."]);

    private static FeatureFlagDefinition Release(string key) => new(key, FeatureFlagType.Release, "Something new.");
}
