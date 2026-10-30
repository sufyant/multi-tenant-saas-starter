using ControlPlane.Contracts.Users;
using Notifications.Application.Notifications;
using Notifications.Domain;
using Notifications.Domain.Notifications;
using SharedKernel;

namespace Notifications.UnitTests.Notifications;

// U2: a notification's title and body are rendered from the texts of packages/emails, in the reader's language and time zone, with its
// stored values and its people named by the caller now.
public sealed class NotificationTextTests
{
    private static readonly Guid Ada = new("0199c000-0000-7000-8000-00000000ada0");

    private static readonly Dictionary<Guid, UserName> People = new() { [Ada] = new UserName("Ada Lovelace", Deleted: false) };

    public static TheoryData<string, Language> EveryTypeInEveryLanguage =>
        [.. NotificationCatalog.Kinds.Keys.SelectMany(type => Enum.GetValues<Language>().Select(language => (type, language)))];

    [Theory]
    [MemberData(nameof(EveryTypeInEveryLanguage))]
    public void Render_EveryTypeInEveryLanguage_FillsEveryPlaceholder(string type, Language language)
    {
        var text = NotificationText.Render(type, SampleValues(type), People, language, timeZone: null);

        $"{text.Title} {text.Body}".ShouldNotContain("{");
        $"{text.Title} {text.Body}".ShouldContain("Acme");
    }

    [Theory]
    [InlineData(Language.En, "Your role in Acme changed", "Ada Lovelace changed your role from Member to Admin.")]
    [InlineData(Language.Tr, "Acme kuruluşundaki rolünüz değişti", "Ada Lovelace rolünüzü Üye rolünden Yönetici rolüne değiştirdi.")]
    [InlineData(Language.Ar, "تغيّر دورك في Acme", "غُيّر دورك من عضو إلى مشرف بواسطة Ada Lovelace.")]
    public void Render_ARoleChangeByAMember_NamesThemAndBothRolesInTheLanguage(Language language, string title, string body)
    {
        var text = NotificationText.Render(NotificationCatalog.MemberRoleChanged, SampleValues(NotificationCatalog.MemberRoleChanged), People, language, null);

        text.ShouldBe(new NotificationText(title, body));
    }

    [Theory]
    [InlineData(Language.En, "The platform changed your role from Member to Owner.")]
    [InlineData(Language.Tr, "Platform, rolünüzü Üye rolünden Sahip rolüne değiştirdi.")]
    [InlineData(Language.Ar, "غيّرت المنصة دورك من عضو إلى مالك.")]
    public void Render_ARoleChangeByThePlatform_NamesThePlatform(Language language, string body)
    {
        var values = new Dictionary<string, string>
        {
            ["tenantName"] = "Acme",
            ["oldRoleName"] = "member",
            ["oldRoleNameBuiltIn"] = "true",
            ["newRoleName"] = "owner",
            ["newRoleNameBuiltIn"] = "true",
        };

        NotificationText.Render(NotificationCatalog.MemberRoleChanged, values, People, language, null).Body.ShouldBe(body);
    }

    [Fact]
    public void Render_ACustomRole_WritesItAsTheOwnerTyped()
    {
        var values = SampleValues(NotificationCatalog.MemberJoined);
        values["roleName"] = "Billing clerk";
        values["roleNameBuiltIn"] = "false";

        var text = NotificationText.Render(NotificationCatalog.MemberJoined, values, People, Language.En, null);

        text.Body.ShouldBe("Ada Lovelace accepted your invitation and joined as Billing clerk.");
    }

    [Theory]
    [InlineData(Language.En, "Deleted user")]
    [InlineData(Language.Tr, "Silinmiş kullanıcı")]
    [InlineData(Language.Ar, "مستخدم محذوف")]
    public void Render_ADeletedPerson_NamesThemDeletedUserInTheLanguage(Language language, string name)
    {
        var deleted = new Dictionary<Guid, UserName> { [Ada] = new UserName(null, Deleted: true) };

        NotificationText.Render(NotificationCatalog.TenantSuspended, SampleValues(NotificationCatalog.TenantSuspended), deleted, language, null).Body
            .ShouldContain(name);
    }

    [Fact]
    public void Render_APersonTheDirectoryDoesNotKnow_NamesThemDeletedUser()
    {
        var text = NotificationText.Render(NotificationCatalog.TenantSuspended, SampleValues(NotificationCatalog.TenantSuspended), new Dictionary<Guid, UserName>(), Language.En, null);

        text.Body.ShouldBe("Deleted user suspended Acme. Its members can’t open it until it is reactivated.");
    }

    [Fact]
    public void Render_APersonWithoutAName_NamesThemUnnamedUser()
    {
        var unnamed = new Dictionary<Guid, UserName> { [Ada] = new UserName(null, Deleted: false) };

        var text = NotificationText.Render(NotificationCatalog.TenantSuspended, SampleValues(NotificationCatalog.TenantSuspended), unnamed, Language.Tr, null);

        text.Body.ShouldBe("Adsız kullanıcı, Acme kuruluşunu askıya aldı. Üyeleri, kuruluş yeniden etkinleştirilene kadar onu açamaz.");
    }

    [Theory]
    [InlineData(Language.En, "The platform set Members limit to 50 for your organisation. Your plan's value applies again on 1 Jan 2027.")]
    [InlineData(Language.Tr, "Platform, kuruluşunuz için Üye sınırı değerini 50 yaptı. Planınızın değeri 1 Oca 2027 tarihinde yeniden geçerli olur.")]
    public void Render_AnOverrideWithAnEnd_WritesTheKeyTheValueAndTheEnd(Language language, string body)
    {
        var values = new Dictionary<string, string>
        {
            ["tenantName"] = "Acme",
            ["keyName"] = "members.max",
            ["value"] = "50",
            ["expiresAt"] = "2027-01-01T00:00:00.0000000+00:00",
        };

        NotificationText.Render(NotificationCatalog.TenantEntitlementOverrideSet, values, People, language, null).Body.ShouldBe(body);
    }

    [Theory]
    [InlineData("on", "The platform set Custom roles to on for your organisation, with no end date.")]
    [InlineData("unlimited", "The platform set Custom roles to unlimited for your organisation, with no end date.")]
    public void Render_AnOverrideWithoutAnEnd_TakesTheTextWithoutTheEnd(string value, string body)
    {
        var values = new Dictionary<string, string> { ["tenantName"] = "Acme", ["keyName"] = "roles.custom", ["value"] = value };

        NotificationText.Render(NotificationCatalog.TenantEntitlementOverrideSet, values, People, Language.En, null).Body.ShouldBe(body);
    }

    [Theory]
    [InlineData(Language.En, "The platform removed the exception for Members limit. Your plan’s value applies again: not set.")]
    [InlineData(Language.Tr, "Platform, Üye sınırı için tanınan istisnayı kaldırdı. Planınızın değeri yeniden geçerli: ayarlanmadı.")]
    [InlineData(Language.Ar, "أزالت المنصة الاستثناء الخاص بـحد الأعضاء. تُطبَّق قيمة خطتك مجددًا: غير محدد.")]
    public void Render_ARemovedOverrideOfAKeyThePlanHasNoValueOf_WritesNotSet(Language language, string body)
    {
        var values = new Dictionary<string, string> { ["tenantName"] = "Acme", ["keyName"] = "members.max", ["value"] = "not_set" };

        NotificationText.Render(NotificationCatalog.TenantEntitlementOverrideRemoved, values, People, language, null).Body.ShouldBe(body);
    }

    // Noon in UTC on 29 November is already 30 November in Auckland (UTC+13).
    [Theory]
    [InlineData(null, "Ada Lovelace closed Acme. It will be deleted on 29 Nov 2026 unless someone reopens it.")]
    [InlineData("Pacific/Auckland", "Ada Lovelace closed Acme. It will be deleted on 30 Nov 2026 unless someone reopens it.")]
    public void Render_AClosedTenant_WritesTheDeletionDateInTheReadersTimeZone(string? timeZone, string body)
    {
        var values = SampleValues(NotificationCatalog.TenantClosed);
        values["reopenableUntil"] = "2026-11-29T12:00:00.0000000+00:00";

        NotificationText.Render(NotificationCatalog.TenantClosed, values, People, Language.En, timeZone).Body.ShouldBe(body);
    }

    [Fact]
    public void Render_AKeyTheTextsDoNotHave_IsAMistakeOfOurs()
    {
        var values = new Dictionary<string, string> { ["tenantName"] = "Acme", ["keyName"] = "storage.max", ["value"] = "5" };

        var render = () => NotificationText.Render(NotificationCatalog.TenantEntitlementOverrideRemoved, values, People, Language.En, null);

        render.ShouldThrow<InvalidOperationException>().Message.ShouldContain("storage.max");
    }

    // A value of each kind, in the names the catalog gives them: Ada for a person, a built-in role, the first word of a closed list, a key,
    // a number for an entitlement, a moment for a date, and a name otherwise. Every optional value is there.
    internal static Dictionary<string, string> SampleValues(string type) =>
        NotificationCatalog.Of(type).Values
            .SelectMany(value => value.Kind switch
            {
                NotificationValueKind.Person => new[] { (value.Name, Ada.ToString()) },
                NotificationValueKind.Role => [(value.Name, value.Name.StartsWith("old", StringComparison.Ordinal) ? "member" : "admin"), ($"{value.Name}BuiltIn", "true")],
                NotificationValueKind.Word => [(value.Name, value.Words![0])],
                NotificationValueKind.EntitlementKey => [(value.Name, "members.max")],
                NotificationValueKind.Entitlement => [(value.Name, "50")],
                NotificationValueKind.Date => [(value.Name, "2026-11-29T12:00:00.0000000+00:00")],
                _ => [(value.Name, value.Name == "tenantName" ? "Acme" : "Pro")],
            })
            .ToDictionary(pair => pair.Item1, pair => pair.Item2);
}
