using Notifications.Application.Notifications;
using Notifications.Domain;
using Notifications.Domain.Notifications;
using SharedKernel;

namespace Notifications.UnitTests.Notifications;

// U2: a notification's title and body are rendered from the texts of packages/emails, in the reader's language, with its stored values.
public sealed class NotificationTextTests
{
    public static TheoryData<string, Language> EveryTypeInEveryLanguage =>
        [.. NotificationCatalog.Kinds.Keys.SelectMany(type => Enum.GetValues<Language>().Select(language => (type, language)))];

    [Theory]
    [MemberData(nameof(EveryTypeInEveryLanguage))]
    public void Render_EveryTypeInEveryLanguage_FillsEveryPlaceholder(string type, Language language)
    {
        var text = NotificationText.Render(type, SampleValues(type), language);

        $"{text.Title} {text.Body}".ShouldNotContain("{");
        $"{text.Title} {text.Body}".ShouldContain("Acme");
    }

    [Theory]
    [InlineData(Language.En, "Your role changed in Acme", "You are now Admin in Acme.")]
    [InlineData(Language.Tr, "Acme kuruluşundaki rolünüz değişti", "Acme kuruluşunda artık Yönetici rolündesiniz.")]
    public void Render_ABuiltInRole_WritesItInTheLanguage(Language language, string title, string body)
    {
        var text = NotificationText.Render(NotificationCatalog.MemberRoleChanged, SampleValues(NotificationCatalog.MemberRoleChanged), language);

        text.ShouldBe(new NotificationText(title, body));
    }

    [Fact]
    public void Render_ACustomRole_WritesItAsTheOwnerTyped()
    {
        var values = new Dictionary<string, string> { ["tenantName"] = "Acme", ["role"] = "Billing clerk", ["roleBuiltIn"] = "false" };

        var text = NotificationText.Render(NotificationCatalog.MemberRoleChanged, values, Language.Tr);

        text.Body.ShouldBe("Acme kuruluşunda artık Billing clerk rolündesiniz.");
    }

    [Fact]
    public void Render_AStatusWord_WritesItInTheLanguage()
    {
        var values = new Dictionary<string, string> { ["tenantName"] = "Acme", ["status"] = NotificationCatalog.StatusClosed };

        var text = NotificationText.Render(NotificationCatalog.TenantStatusChanged, values, Language.Tr);

        text.Title.ShouldBe("Acme kapatıldı");
    }

    [Fact]
    public void Render_AWordTheTextsDoNotHave_IsAMistakeOfOurs()
    {
        var values = new Dictionary<string, string> { ["tenantName"] = "Acme", ["status"] = "archived" };

        var render = () => NotificationText.Render(NotificationCatalog.TenantStatusChanged, values, Language.En);

        render.ShouldThrow<InvalidOperationException>().Message.ShouldContain("archived");
    }

    // A value of each kind, in the names the catalog gives them: a built-in role, the first word of a closed list, and a name otherwise.
    internal static Dictionary<string, string> SampleValues(string type) =>
        NotificationCatalog.Of(type).Values
            .SelectMany(value => value.Kind switch
            {
                NotificationValueKind.Role => new[] { (value.Name, "admin"), ($"{value.Name}BuiltIn", "true") },
                NotificationValueKind.Word => [(value.Name, value.Words![0])],
                _ => [(value.Name, value.Name == "tenantName" ? "Acme" : "Ayşe Yılmaz")],
            })
            .ToDictionary(pair => pair.Item1, pair => pair.Item2);
}
