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

    // A type about a person's own account, such as user.data_export_ready, names no tenant.
    public static TheoryData<string, Language> EveryTypeAboutATenantInEveryLanguage =>
        [.. NotificationCatalog.Kinds.Values
            .Where(kind => kind.Values.Any(value => value.Name == "tenantName"))
            .SelectMany(kind => Enum.GetValues<Language>().Select(language => (kind.Type, language)))];

    [Theory]
    [MemberData(nameof(EveryTypeInEveryLanguage))]
    public void Render_EveryTypeInEveryLanguage_FillsEveryPlaceholder(string type, Language language)
    {
        var text = NotificationText.Render(type, SampleValues(type), People, language, timeZone: null);

        $"{text.Title} {text.Body}".ShouldNotContain("{");
        text.Body.ShouldNotBeNullOrWhiteSpace();
    }

    // The bell names the tenant beside each item; a type's texts or its email's footer name it too.
    [Theory]
    [MemberData(nameof(EveryTypeAboutATenantInEveryLanguage))]
    public void Render_EveryTypeAboutATenant_NamesTheTenant(string type, Language language) =>
        $"{NotificationText.RenderEmail(type, SampleValues(type), People, language, timeZone: null)} {Footer(type, language)}".ShouldContain("Acme");

    [Theory]
    [MemberData(nameof(EveryTypeInEveryLanguage))]
    public void RenderEmail_EveryTypeInEveryLanguage_FillsEveryPlaceholderAndHasAButton(string type, Language language)
    {
        var text = NotificationText.RenderEmail(type, SampleValues(type), People, language, timeZone: null);

        $"{text.Title} {text.Body} {text.Button}".ShouldNotContain("{");
        text.Button.ShouldNotBeNullOrWhiteSpace();
    }

    // 13.18 and 3.17: the email says more than the bell, and its button is the type's own.
    [Theory]
    [InlineData(
        Language.En,
        "We’ll try again on 5 Nov 2026. Update the card in Billing.",
        "Stripe tried Visa ending in 4242 on 1 Nov 2026 for €100 excl. VAT. We’ll try again on 5 Nov 2026. Everything keeps working. Update the card to pay now.",
        "Update card")]
    [InlineData(
        Language.Tr,
        "5 Kas 2026 tarihinde yeniden deneyeceğiz. Kartı Faturalama’da güncelleyin.",
        "Stripe, 1 Kas 2026 tarihinde 4242 ile biten Visa kartından KDV hariç €100 tahsil etmeyi denedi. 5 Kas 2026 tarihinde yeniden deneyeceğiz. Her şey çalışmaya devam ediyor. Şimdi ödemek için kartı güncelleyin.",
        "Kartı güncelle")]
    public void RenderEmail_AFailedPayment_SaysTheCardTheAmountAndTheNextAttempt(Language language, string bell, string email, string button)
    {
        var values = new Dictionary<string, string>
        {
            ["tenantName"] = "Acme",
            ["amountDue"] = NotificationText.MoneyValue(10000, "EUR"),
            ["failedAt"] = "2026-11-01T09:00:00.0000000+00:00",
            ["nextAttemptAt"] = "2026-11-05T09:00:00.0000000+00:00",
            ["cardBrand"] = "visa",
            ["cardLast4"] = "4242",
        };

        var shown = NotificationText.Render(NotificationCatalog.TenantPaymentFailed, values, People, language, null);
        var sent = NotificationText.RenderEmail(NotificationCatalog.TenantPaymentFailed, values, People, language, null);

        shown.Body.ShouldBe(bell);
        (sent.Body, sent.Button).ShouldBe((email, button));
    }

    [Fact]
    public void RenderEmail_AFailedPaymentWithoutACard_SaysTheAmountWithoutTheCard()
    {
        var values = new Dictionary<string, string>
        {
            ["tenantName"] = "Acme",
            ["amountDue"] = NotificationText.MoneyValue(1250, "EUR"),
            ["failedAt"] = "2026-11-01T09:00:00.0000000+00:00",
            ["nextAttemptAt"] = "2026-11-05T09:00:00.0000000+00:00",
        };

        var sent = NotificationText.RenderEmail(NotificationCatalog.TenantPaymentFailed, values, People, Language.En, null);

        sent.Body.ShouldBe("Stripe tried to take €12.50 excl. VAT on 1 Nov 2026. We’ll try again on 5 Nov 2026. Everything keeps working. Update the card to pay now.");
    }

    // A number of attempts takes the form of its plural category (U2): "once", "4 times"; in Arabic "مرتين" for two.
    [Theory]
    [InlineData(Language.En, "1", "The payment failed once.")]
    [InlineData(Language.En, "4", "The payment failed 4 times.")]
    [InlineData(Language.Tr, "4", "Ödeme 4 kez başarısız oldu.")]
    [InlineData(Language.Ar, "2", "فشل الدفع مرتين.")]
    public void Render_TheLastWarning_WritesTheAttemptsInTheirPluralForm(Language language, string attempts, string body)
    {
        var values = SampleValues(NotificationCatalog.TenantPaymentGraceStarted);
        values["attempts"] = attempts;

        NotificationText.Render(NotificationCatalog.TenantPaymentGraceStarted, values, People, language, null).Body.ShouldBe(body);
    }

    // A brand the catalog lists is named; the handler writes any other as unknown, which the texts write as "the card".
    [Fact]
    public void RenderEmail_TheLastWarningWithAnUnknownCard_SaysTheCard()
    {
        var values = SampleValues(NotificationCatalog.TenantPaymentGraceStarted);
        values["cardBrand"] = NotificationCatalog.UnknownCard;
        values["cardLast4"] = "0005";

        var sent = NotificationText.RenderEmail(NotificationCatalog.TenantPaymentGraceStarted, values, People, Language.En, null);

        sent.Body.ShouldStartWith("We tried the card ending in 0005 4 times since 29 Nov 2026.");
    }

    // 13.20: the open invoice pays the plan back; without one, the email asks for a plan.
    [Fact]
    public void RenderEmail_AMoveToTheDefaultPlanWithAnOpenInvoice_NamesItsAmount()
    {
        var values = MovedToFree();
        values["amountDue"] = NotificationText.MoneyValue(10000, "EUR");

        var sent = NotificationText.RenderEmail(NotificationCatalog.TenantMovedToDefaultPlan, values, People, Language.En, null);

        sent.ShouldBe(new NotificationEmailText(
            "Acme moved to Free",
            "We couldn’t take the payment for Team, so Acme moved to Free on 20 Nov 2026. Nothing was deleted. Pay the open invoice of €100 excl. VAT to get Team back at once.",
            "Pay now"));
    }

    [Fact]
    public void RenderEmail_AMoveToTheDefaultPlanWithoutAnOpenInvoice_AsksForAPlan()
    {
        var sent = NotificationText.RenderEmail(NotificationCatalog.TenantMovedToDefaultPlan, MovedToFree(), People, Language.En, null);

        sent.Body.ShouldBe("We couldn’t take the payment for Team, so Acme moved to Free on 20 Nov 2026. Nothing was deleted. Pay for a plan to get Team back at once.");
    }

    // 13.21 and 3.17: the fee that changes names its old amount, and one that stays says so; the amounts are of the tenant's interval.
    [Fact]
    public void RenderEmail_APriceChangeOfTheFeePerPerson_SaysTheBaseFeeStays()
    {
        var values = PriceChange();
        values["oldPerPerson"] = NotificationText.MoneyValue(1000, "EUR");

        var shown = NotificationText.Render(NotificationCatalog.TenantPriceChangeAnnounced, values, People, Language.En, null);
        var sent = NotificationText.RenderEmail(NotificationCatalog.TenantPriceChangeAnnounced, values, People, Language.En, null);

        shown.ShouldBe(new NotificationText("Team’s price changes on 1 Feb 2027", "€12 per person a month instead of €10, excl. VAT."));
        sent.Body.ShouldBe(
            "From 1 Feb 2027 the Team fee per person goes from €10 to €12 a month, excl. VAT. The base fee stays €0. For your 10 people: €120 a month instead of €100, excl. VAT. Nothing changes before 1 Feb 2027. You can change or cancel your plan at any time.");
        sent.Button.ShouldBe("Open Billing");
    }

    [Fact]
    public void RenderEmail_AYearlyPriceChangeOfTheBaseFee_SaysTheFeePerPersonStays()
    {
        var values = PriceChange();
        values["period"] = NotificationCatalog.Year;
        values["oldBase"] = NotificationText.MoneyValue(0, "EUR");
        values["base"] = NotificationText.MoneyValue(99000, "EUR");
        values["perPerson"] = NotificationText.MoneyValue(12000, "EUR");
        values["newTotal"] = NotificationText.MoneyValue(219000, "EUR");
        values["oldTotal"] = NotificationText.MoneyValue(120000, "EUR");

        var sent = NotificationText.RenderEmail(NotificationCatalog.TenantPriceChangeAnnounced, values, People, Language.En, null);

        sent.Body.ShouldBe(
            "From 1 Feb 2027 the Team base fee goes from €0 to €990 a year, excl. VAT. The fee per person stays €120. For your 10 people: €2,190 a year instead of €1,200, excl. VAT. Nothing changes before 1 Feb 2027. You can change or cancel your plan at any time.");
    }

    [Theory]
    [InlineData(Language.En, "€12 per person and a base fee of €99 a month instead of €10 and €89, excl. VAT.")]
    [InlineData(Language.Tr, "Kişi başı ayda €10 yerine €12, temel ücret €89 yerine €99, KDV hariç.")]
    [InlineData(Language.Ar, "\u200f12\u00a0€ لكل شخص ورسم أساسي \u200f99\u00a0€ شهريًا بدلًا من \u200f10\u00a0€ و\u200f89\u00a0€، دون ضريبة القيمة المضافة.")]
    public void Render_APriceChangeOfBothFees_NamesBothInTheLanguage(Language language, string body)
    {
        var values = PriceChange();
        values["oldPerPerson"] = NotificationText.MoneyValue(1000, "EUR");
        values["oldBase"] = NotificationText.MoneyValue(8900, "EUR");
        values["base"] = NotificationText.MoneyValue(9900, "EUR");

        NotificationText.Render(NotificationCatalog.TenantPriceChangeAnnounced, values, People, language, null).Body.ShouldBe(body);
    }

    // Money is written as the apps' formatMoney writes it (packages/ui, format-money.test.ts): no decimals for a whole amount, the
    // language's marks, and the currency's sign where the language puts it.
    [Theory]
    [InlineData(Language.En, 123456789, "EUR", "€1,234,567.89")]
    [InlineData(Language.Tr, 123456789, "EUR", "€1.234.567,89")]
    [InlineData(Language.Ar, 1200, "EUR", "\u200f12\u00a0€")]
    [InlineData(Language.En, 1205, "USD", "$12.05")]
    [InlineData(Language.En, 1200, "JPY", "¥1,200")]
    public void Render_AnAmount_IsWrittenAsTheAppsWriteIt(Language language, long minor, string currency, string written)
    {
        var values = MovedToFree();
        values["amountDue"] = NotificationText.MoneyValue(minor, currency);

        NotificationText.RenderEmail(NotificationCatalog.TenantMovedToDefaultPlan, values, People, language, null).Body.ShouldContain(written);
    }

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
                NotificationValueKind.Day => [(value.Name, "2026-12-01")],
                NotificationValueKind.Days => [(value.Name, "30")],
                NotificationValueKind.FeatureFlags => [(value.Name, "2")],
                NotificationValueKind.Attempts => [(value.Name, "4")],
                NotificationValueKind.People => [(value.Name, "10")],
                NotificationValueKind.Money => [(value.Name, "10000 EUR")],
                _ => [(value.Name, value.Name == "tenantName" ? "Acme" : "Pro")],
            })
            .ToDictionary(pair => pair.Item1, pair => pair.Item2);

    private static Dictionary<string, string> MovedToFree() => new()
    {
        ["tenantName"] = "Acme",
        ["planName"] = "Team",
        ["defaultPlanName"] = "Free",
        ["movedAt"] = "2026-11-20T09:00:00.0000000+00:00",
    };

    private static Dictionary<string, string> PriceChange() => new()
    {
        ["tenantName"] = "Acme",
        ["planName"] = "Team",
        ["movesAt"] = "2027-02-01T09:00:00.0000000+00:00",
        ["period"] = NotificationCatalog.Month,
        ["perPerson"] = NotificationText.MoneyValue(1200, "EUR"),
        ["base"] = NotificationText.MoneyValue(0, "EUR"),
        ["people"] = "10",
        ["newTotal"] = NotificationText.MoneyValue(12000, "EUR"),
        ["oldTotal"] = NotificationText.MoneyValue(10000, "EUR"),
    };

    private static string Footer(string type, Language language) =>
        NotificationText.HasFooter(language, type) ? NotificationText.FooterOf(type, SampleValues(type), People, language, null) : "";
}
