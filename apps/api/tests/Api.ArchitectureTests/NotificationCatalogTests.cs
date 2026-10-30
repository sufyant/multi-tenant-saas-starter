using System.Reflection;
using Audit.Domain;
using ControlPlane.Contracts.Plans;
using Notifications.Application.Notifications;
using Notifications.Domain;
using Notifications.Domain.Notifications;
using SharedKernel;

namespace Api.ArchitectureTests;

// U1, U2: a notification type is a template. A type of the catalog has a category of the catalog, its texts in every language with exactly
// the values it declares, a text without each optional value, every word of a closed list in every language, and one handler that names it
// (its const Type). So a new type needs only its entry, its texts and its handler, and a type that misses one fails here. A planted catalog
// shows that each gap is found; a new entitlement key of ControlPlane fails here until the texts name it. U9: a type has the name of its event's audit operation, or is named apart with its reason.
public sealed class NotificationCatalogTests
{
    [Fact]
    public void DeclareNotificationTypes_TheCatalog_HasNoGap()
    {
        var gaps = Gaps(NotificationCatalog.Kinds.Values, NotificationCatalog.Categories, HandlerTypes(typeof(NotificationText).Assembly), TextsOf);

        NotificationCatalog.Kinds.ShouldNotBeEmpty();
        gaps.ShouldBeEmpty();
    }

    [Fact]
    public void DeclareNotificationTypes_APlantedTypeWithoutTextsHandlerOrCategory_IsFound()
    {
        NotificationKind planted = new(
            "tenant.renamed",
            "billing",
            NotificationBell.Tenant,
            NotificationLinkKind.TenantSettings,
            new Dictionary<NotificationChannel, bool>(),
            Required: false,
            [new("status", NotificationValueKind.Word, ["archived"]), new("actorName", NotificationValueKind.Person, Optional: true)]);

        var gaps = Gaps([planted], NotificationCatalog.Categories, HandlerTypes(typeof(NotificationText).Assembly), TextsOf);

        gaps.ShouldBe(
        [
            "tenant.renamed: the category billing is not in the catalog",
            "tenant.renamed: 0 handlers name it",
            "tenant.renamed: no texts in en",
            "tenant.renamed: no texts in tr",
            "tenant.renamed: no texts in ar",
        ]);
    }

    [Fact]
    public void DeclareNotificationTypes_APlantedTypeWithAValueItsTextsDoNotUse_IsFound()
    {
        var kind = NotificationCatalog.Of(NotificationCatalog.TenantPurged) with
        {
            Values = [new("tenantName", NotificationValueKind.Text), new("ownerName", NotificationValueKind.Person)],
        };

        var gaps = Gaps([kind], NotificationCatalog.Categories, HandlerTypes(typeof(NotificationText).Assembly), TextsOf);

        gaps.ShouldBe(
        [
            "tenant.purged: the texts in en use ownerName, tenantName? they use tenantName",
            "tenant.purged: the texts in tr use ownerName, tenantName? they use tenantName",
            "tenant.purged: the texts in ar use ownerName, tenantName? they use tenantName",
        ]);
    }

    [Fact]
    public void DeclareNotificationTypes_APlantedOptionalValueWithoutItsText_IsFound()
    {
        var kind = NotificationCatalog.Of(NotificationCatalog.TenantSuspended) with
        {
            Values = [new("tenantName", NotificationValueKind.Text), new("actorName", NotificationValueKind.Person, Optional: true)],
        };

        var gaps = Gaps([kind], NotificationCatalog.Categories, HandlerTypes(typeof(NotificationText).Assembly), TextsOf);

        gaps.ShouldBe(
        [
            "tenant.suspended: no text without actorName in en",
            "tenant.suspended: no text without actorName in tr",
            "tenant.suspended: no text without actorName in ar",
        ]);
    }

    // U5: each category has its name and its description in every language, which the email's footer and the preferences screen show.
    [Fact]
    public void DeclareNotificationCategories_EveryCategory_HasItsNameAndDescriptionInEveryLanguage()
    {
        var missing = NotificationCatalog.Categories
            .SelectMany(category => Enum.GetValues<Language>().Select(language => (category, language, texts: NotificationText.CategoryOf(language, category))))
            .Where(found => found.texts is not { Name.Length: > 0, Description.Length: > 0 })
            .Select(found => $"{found.category} in {JsonEnum<Language>.NameOf(found.language)}");

        missing.ShouldBeEmpty();
    }

    // U9: the same event writes an audit entry and a notification, with one label. A type of another name says why.
    [Fact]
    public void NameNotificationTypes_EveryType_HasTheNameOfAnAuditOperationOrIsNamedApart()
    {
        var operations = OperationsOf(typeof(AuditOperations));

        var unnamed = NotificationCatalog.Kinds.Keys
            .Where(type => !operations.Contains(type) && !NotificationCatalog.NamedApart.ContainsKey(type))
            .ToList();

        unnamed.ShouldBeEmpty();
        NotificationCatalog.NamedApart.Keys.Where(operations.Contains).ShouldBeEmpty();
        NotificationCatalog.NamedApart.Values.ShouldAllBe(reason => reason.Length > 0);
    }

    // The values of a class's string constants.
    private static HashSet<string> OperationsOf(Type type) =>
        [.. type.GetFields(BindingFlags.Public | BindingFlags.Static).Where(field => field.IsLiteral).Select(field => (string)field.GetRawConstantValue()!)];

    // The handlers of the module: each static class whose const Type names a notification type.
    private static IReadOnlyList<(Type Handler, string Type)> HandlerTypes(Assembly assembly) =>
        [.. assembly.GetTypes()
            .Where(type => type is { IsAbstract: true, IsSealed: true } && type.Name.EndsWith("Handler", StringComparison.Ordinal))
            .Select(type => (type, Field: type.GetField("Type", BindingFlags.Public | BindingFlags.Static)))
            .Where(found => found.Field is { IsLiteral: true })
            .Select(found => (found.type, (string)found.Field!.GetRawConstantValue()!))];

    // The texts the generated file has for a type in a language: the values its main texts use, or its texts without an optional value, and
    // the words of each closed list; none when it has no texts.
    private static Texts? TextsOf(string type, Language language) =>
        NotificationText.TypesOf(language).Contains(type)
            ? new Texts(without => NotificationText.PlaceholdersOf(language, type, without), list => NotificationText.WordsOf(language, list))
            : null;

    private static List<string> Gaps(
        IEnumerable<NotificationKind> kinds,
        IReadOnlyList<string> categories,
        IReadOnlyList<(Type Handler, string Type)> handlers,
        Func<string, Language, Texts?> textsOf)
    {
        List<string> gaps = [];
        foreach (var kind in kinds)
        {
            if (!categories.Contains(kind.Category))
            {
                gaps.Add($"{kind.Type}: the category {kind.Category} is not in the catalog");
            }

            var named = handlers.Count(handler => handler.Type == kind.Type);
            if (named != 1)
            {
                gaps.Add($"{kind.Type}: {named} handlers name it");
            }

            foreach (var language in Enum.GetValues<Language>())
            {
                gaps.AddRange(TextGaps(kind, language, textsOf(kind.Type, language)));
            }
        }

        return gaps;
    }

    private static IEnumerable<string> TextGaps(NotificationKind kind, Language language, Texts? texts)
    {
        var tag = JsonEnum<Language>.NameOf(language);
        if (texts is not { } found)
        {
            return [$"{kind.Type}: no texts in {tag}"];
        }

        var declared = kind.Values.Select(value => value.Name).ToList();
        var placeholderGaps = PlaceholderGaps(kind.Type, tag, declared, found.Placeholders(null));
        var withoutGaps = kind.Values.Where(value => value.Optional).SelectMany(optional => found.Placeholders(optional.Name) is { } used
            ? PlaceholderGaps($"{kind.Type} without {optional.Name}", tag, declared.Where(name => name != optional.Name), used)
            : [$"{kind.Type}: no text without {optional.Name} in {tag}"]);
        var wordGaps = kind.Values
            .SelectMany(value => WordsOf(value).Select(word => (value.Kind == NotificationValueKind.Word ? value.Name : ListOf(value.Kind), word)))
            .Where(word => !found.Words(word.Item1).Contains(word.word))
            .Select(word => $"{kind.Type}: no word {word.word} of {word.Item1} in {tag}");
        return placeholderGaps.Concat(withoutGaps).Concat(wordGaps).Distinct();
    }

    private static IEnumerable<string> PlaceholderGaps(string what, string tag, IEnumerable<string> declared, IReadOnlySet<string>? used)
    {
        var expected = declared.Order(StringComparer.Ordinal).ToList();
        var actual = (used ?? new HashSet<string>()).Order(StringComparer.Ordinal).ToList();
        return expected.SequenceEqual(actual)
            ? []
            : [$"{what}: the texts in {tag} use {string.Join(", ", expected)}? they use {string.Join(", ", actual)}"];
    }

    // The words each value needs in every language: its closed list, the words of an entitlement's value, the name of every key of
    // ControlPlane's entitlement list, or a person the read cannot name.
    private static IEnumerable<string> WordsOf(NotificationValue value) => value.Kind switch
    {
        NotificationValueKind.Word => value.Words ?? [],
        NotificationValueKind.Entitlement => ["unlimited", "on", "off", "not_set"],
        NotificationValueKind.EntitlementKey => OperationsOf(typeof(Features)).Concat(OperationsOf(typeof(Limits))),
        NotificationValueKind.Person => ["deleted", "unnamed"],
        _ => [],
    };

    private static string ListOf(NotificationValueKind kind) => kind switch
    {
        NotificationValueKind.Entitlement => NotificationText.EntitlementWords,
        NotificationValueKind.EntitlementKey => NotificationText.EntitlementKeyWords,
        _ => NotificationText.PersonWords,
    };

    private sealed record Texts(Func<string?, IReadOnlySet<string>?> Placeholders, Func<string, IReadOnlyCollection<string>> Words);
}
