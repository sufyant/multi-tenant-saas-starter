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

    // A day is the same for every reader: a terms version takes effect on 1 December wherever the reader is, so no time zone moves it.
    [Theory]
    [InlineData("America/Los_Angeles")]
    [InlineData("Pacific/Kiritimati")]
    public void Render_ATermsVersionOfADay_WritesTheDayInNoTimeZone(string timeZone) =>
        NotificationText.Render(
                NotificationCatalog.TermsVersionCreated,
                new Dictionary<string, string> { ["effectiveDate"] = "2026-12-01", ["url"] = "https://brand.example/terms", ["nextStep"] = NotificationCatalog.NothingToDo },
                People,
                Language.En,
                timeZone)
            .Title.ShouldBe("Our terms of use change on 1 Dec 2026");

    [Fact]
    public void Render_ATermsVersionWithoutASummary_ReadsWithoutIt() =>
        NotificationText.Render(
                NotificationCatalog.TermsVersionCreated,
                new Dictionary<string, string> { ["effectiveDate"] = "2026-12-01", ["url"] = "https://brand.example/terms", ["nextStep"] = NotificationCatalog.NothingToDo },
                People,
                Language.En,
                timeZone: null)
            .Body.ShouldBe("The new terms apply from 1 Dec 2026. You do not need to do anything. Read the terms: https://brand.example/terms");

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
    [InlineData(Language.En, "The platform set Members limit to 50 for your organisation. Your plan’s value applies again on 1 Jan 2027.")]
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

    // 3.07: the weekly count of the flags takes its noun in the plural form of the number's CLDR category, as a number of days does.
    [Theory]
    [InlineData(Language.En, "1", "1 feature flag ready to be removed from code")]
    [InlineData(Language.En, "2", "2 feature flags ready to be removed from code")]
    [InlineData(Language.Tr, "2", "2 özellik bayrağı koddan kaldırılmaya hazır")]
    [InlineData(Language.Ar, "1", "علامة ميزات واحدة جاهزة للإزالة من الشيفرة")]
    [InlineData(Language.Ar, "2", "علامتا ميزات جاهزة للإزالة من الشيفرة")]
    [InlineData(Language.Ar, "3", "3 علامات ميزات جاهزة للإزالة من الشيفرة")]
    [InlineData(Language.Ar, "11", "11 علامة ميزات جاهزة للإزالة من الشيفرة")]
    public void Render_FlagsReadyToRemove_WritesTheCountWithItsPluralForm(Language language, string count, string title) =>
        NotificationText.Render(NotificationCatalog.FeatureFlagsReadyToRemove, new Dictionary<string, string> { ["count"] = count }, People, language, null)
            .Title.ShouldBe(title);

    // K14: a plan change that shortens the Activity log period says which entries go and on which date, in the words of 12.10.
    [Theory]
    [InlineData(Language.En, "The platform moved your organisation from Free to Pro. Its features and limits follow the new plan. The Activity log now keeps entries for 90 days. Entries older than 90 days go on 1 Dec 2026. To keep a copy of the older ones, export them before then.")]
    [InlineData(Language.Tr, "Platform, kuruluşunuzu Free planından Pro planına geçirdi. Özellikler ve sınırlar yeni plana göre uygulanır. Etkinlik kaydı artık kayıtları 90 gün saklıyor. 90 günden eski kayıtlar 1 Ara 2026 tarihinde silinir. Eskilerin bir kopyasını tutmak için o tarihten önce dışa aktarın.")]
    [InlineData(Language.Ar, "نقلت المنصة مؤسستك من خطة Free إلى خطة Pro. تتبع الميزات والحدود الخطة الجديدة. يحتفظ سجل النشاط الآن بالإدخالات لمدة 90 يومًا. تُحذف الإدخالات الأقدم من 90 يومًا في 1 ديسمبر 2026. للاحتفاظ بنسخة من الإدخالات الأقدم، صدّرها قبل ذلك التاريخ.")]
    public void Render_APlanChangeThatShortensTheActivityLog_SaysWhichEntriesGoAndWhen(Language language, string body)
    {
        var values = new Dictionary<string, string>
        {
            ["tenantName"] = "Acme",
            ["oldPlanName"] = "Free",
            ["newPlanName"] = "Pro",
            ["retentionDays"] = "90",
            ["retentionGoesAt"] = "2026-12-01T12:00:00.0000000+00:00",
        };

        NotificationText.Render(NotificationCatalog.TenantPlanChanged, values, People, language, null).Body.ShouldBe(body);
    }

    // A number of days takes the plural form of its CLDR category in the language: English "one" and "other", Turkish one form, and
    // Arabic "few" when the last two digits are 3 to 10, "many" when they are 11 to 99, and "other" for the rest, as 100 to 102 (Unicode
    // CLDR, "Language Plural Rules"). The Arabic texts name the number after "لمدة" and "من", so its dual is the genitive.
    [Theory]
    [InlineData(Language.En, "1", "keeps entries for 1 day. Entries older than 1 day go on")]
    [InlineData(Language.En, "30", "keeps entries for 30 days. Entries older than 30 days go on")]
    [InlineData(Language.En, "90", "keeps entries for 90 days. Entries older than 90 days go on")]
    [InlineData(Language.En, "100", "keeps entries for 100 days. Entries older than 100 days go on")]
    [InlineData(Language.En, "365", "keeps entries for 365 days. Entries older than 365 days go on")]
    [InlineData(Language.Tr, "1", "kayıtları 1 gün saklıyor. 1 günden eski kayıtlar")]
    [InlineData(Language.Tr, "30", "kayıtları 30 gün saklıyor. 30 günden eski kayıtlar")]
    [InlineData(Language.Tr, "90", "kayıtları 90 gün saklıyor. 90 günden eski kayıtlar")]
    [InlineData(Language.Tr, "100", "kayıtları 100 gün saklıyor. 100 günden eski kayıtlar")]
    [InlineData(Language.Tr, "365", "kayıtları 365 gün saklıyor. 365 günden eski kayıtlar")]
    [InlineData(Language.Ar, "1", "بالإدخالات لمدة يوم واحد. تُحذف الإدخالات الأقدم من يوم واحد في")]
    [InlineData(Language.Ar, "30", "بالإدخالات لمدة 30 يومًا. تُحذف الإدخالات الأقدم من 30 يومًا في")]
    [InlineData(Language.Ar, "90", "بالإدخالات لمدة 90 يومًا. تُحذف الإدخالات الأقدم من 90 يومًا في")]
    [InlineData(Language.Ar, "100", "بالإدخالات لمدة 100 يوم. تُحذف الإدخالات الأقدم من 100 يوم في")]
    [InlineData(Language.Ar, "365", "بالإدخالات لمدة 365 يومًا. تُحذف الإدخالات الأقدم من 365 يومًا في")]
    public void Render_APlanChangeOfANumberOfDays_WritesThePluralFormOfTheLanguage(Language language, string days, string written)
    {
        var values = new Dictionary<string, string>
        {
            ["tenantName"] = "Acme",
            ["oldPlanName"] = "Free",
            ["newPlanName"] = "Pro",
            ["retentionDays"] = days,
            ["retentionGoesAt"] = "2026-12-01T12:00:00.0000000+00:00",
        };

        NotificationText.Render(NotificationCatalog.TenantPlanChanged, values, People, language, null).Body.ShouldContain(written);
    }

    // Arabic's other categories, which no period of the Activity log reaches (its floor is 30 days), have their forms too.
    [Theory]
    [InlineData("0", "لمدة 0 يوم.")]
    [InlineData("2", "لمدة يومين.")]
    [InlineData("3", "لمدة 3 أيام.")]
    [InlineData("10", "لمدة 10 أيام.")]
    [InlineData("11", "لمدة 11 يومًا.")]
    [InlineData("102", "لمدة 102 يوم.")]
    [InlineData("103", "لمدة 103 أيام.")]
    public void Render_ANumberOfDaysInArabic_WritesTheFormOfEveryCategory(string days, string written)
    {
        var values = new Dictionary<string, string>
        {
            ["tenantName"] = "Acme",
            ["oldPlanName"] = "Free",
            ["newPlanName"] = "Pro",
            ["retentionDays"] = days,
            ["retentionGoesAt"] = "2026-12-01T12:00:00.0000000+00:00",
        };

        NotificationText.Render(NotificationCatalog.TenantPlanChanged, values, People, Language.Ar, null).Body.ShouldContain(written);
    }

    [Theory]
    [InlineData(Language.En, "The platform moved your organisation from Free to Pro. Its features and limits follow the new plan.")]
    [InlineData(Language.Tr, "Platform, kuruluşunuzu Free planından Pro planına geçirdi. Özellikler ve sınırlar yeni plana göre uygulanır.")]
    [InlineData(Language.Ar, "نقلت المنصة مؤسستك من خطة Free إلى خطة Pro. تتبع الميزات والحدود الخطة الجديدة.")]
    public void Render_APlanChangeThatShortensNothing_TakesTheTextWithoutTheActivityLog(Language language, string body)
    {
        var values = new Dictionary<string, string> { ["tenantName"] = "Acme", ["oldPlanName"] = "Free", ["newPlanName"] = "Pro" };

        NotificationText.Render(NotificationCatalog.TenantPlanChanged, values, People, language, null).Body.ShouldBe(body);
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
    // a number for an entitlement, a moment for a date, 30 for a number of days, and a name otherwise. Every optional value is there.
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
