using Tenancy.Caching;

namespace Tenancy.UnitTests.Caching;

// Section 3, "Cache": every key holds the version of the shape its read stores. The version comes from the shape itself, so a change
// that a reader of an old entry could misread changes the key, and a deploy never reads an entry of the old shape.
public sealed class CachedReadTests
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(1);

    public enum State
    {
        Active,
        Suspended,
    }

    public enum RenamedState
    {
        Active,
        Paused,
    }

    public static TheoryData<CachedRead> ChangedShapes => new()
    {
        new UserCachedRead<ShapeWithAnotherProperty>("read", Lifetime, subject => [subject]),
        new UserCachedRead<ShapeWithARenamedProperty>("read", Lifetime, subject => [subject]),
        new UserCachedRead<ShapeWithAnotherType>("read", Lifetime, subject => [subject]),
        new UserCachedRead<ShapeWithAnotherStateType>("read", Lifetime, subject => [subject]),
    };

    [Fact]
    public void Version_TheSameShape_StaysTheSame()
    {
        var first = new UserCachedRead<Shape>("read", Lifetime, subject => [subject]);
        var second = new UserCachedRead<Shape>("read", Lifetime, subject => [subject]);

        first.Version.ShouldBe(second.Version);
    }

    [Theory]
    [MemberData(nameof(ChangedShapes))]
    public void Version_AChangedShape_Changes(CachedRead changed)
    {
        var original = new UserCachedRead<Shape>("read", Lifetime, subject => [subject]);

        changed.Version.ShouldNotBe(original.Version);
    }

    // C3: every part of a key names what it is, in the one form that a tag writes too.
    [Fact]
    public void KeyOf_ATenantReadAndAUserRead_LabelEveryPartAsATagDoes()
    {
        var tenantId = Guid.Parse("0b8f1c2e-6d4a-4f7b-9e3a-2c5d8e1f4a6b");
        var tenantRead = new TenantCachedRead<Shape>("member-access", Lifetime, (tenant, _) => [CacheTag.Of(CacheLabel.Tenant, tenant)]);
        var userRead = new UserCachedRead<Shape>("user-access", Lifetime, user => [CacheTag.Of(CacheLabel.User, user)]);

        var tenantKey = tenantRead.KeyOf(tenantId, "user_2x");
        var userKey = userRead.KeyOf("user_2x");

        tenantKey.ShouldBe($"member-access:{tenantRead.Version}:tenant:{tenantId}:user:user_2x");
        userKey.ShouldBe($"user-access:{userRead.Version}:user:user_2x");
        tenantKey.ShouldContain(tenantRead.TagsOf(tenantId, "user_2x")[0]);
    }

    [Fact]
    public void TagKinds_OfARead_AreTheKindsOfTheTagsItNames()
    {
        var read = new TenantCachedRead<Shape>("read", Lifetime, (tenantId, subject) => [CacheTag.Of("tenant", tenantId), CacheTag.Of("user", subject), "all"]);

        read.TagKinds.ShouldBe(["tenant", "user", "all"]);
    }

    public sealed record Shape(bool Known, State State, string[] Permissions);

    public sealed record ShapeWithAnotherProperty(bool Known, State State, string[] Permissions, bool Deleted);

    public sealed record ShapeWithARenamedProperty(bool Found, State State, string[] Permissions);

    public sealed record ShapeWithAnotherType(int Known, State State, string[] Permissions);

    public sealed record ShapeWithAnotherStateType(bool Known, RenamedState State, string[] Permissions);
}
