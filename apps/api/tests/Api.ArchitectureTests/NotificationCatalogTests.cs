using System.Reflection;
using Notifications.Application.Notifications;
using Notifications.Domain;
using Notifications.Domain.Notifications;
using SharedKernel;

namespace Api.ArchitectureTests;

// U1, U2: a notification type is a template. A type of the catalog has a category of the catalog, its texts in every language with exactly
// the values it declares, every word of a closed list in every language, and one handler that names it (its const Type). So a new type needs
// only its entry, its texts and its handler, and a type that misses one fails here. A planted catalog shows that each gap is found.
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
            NotificationLinkKind.Tenant,
            new Dictionary<NotificationChannel, bool>(),
            Required: false,
            [new("status", NotificationValueKind.Word, ["archived"])]);

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
            Values = [new("tenantName", NotificationValueKind.Text), new("ownerName", NotificationValueKind.Text)],
        };

        var gaps = Gaps([kind], NotificationCatalog.Categories, HandlerTypes(typeof(NotificationText).Assembly), TextsOf);

        gaps.ShouldBe(
        [
            "tenant.purged: the texts in en use ownerName, tenantName? they use tenantName",
            "tenant.purged: the texts in tr use ownerName, tenantName? they use tenantName",
            "tenant.purged: the texts in ar use ownerName, tenantName? they use tenantName",
        ]);
    }

    // The handlers of the module: each static class whose const Type names a notification type.
    private static IReadOnlyList<(Type Handler, string Type)> HandlerTypes(Assembly assembly) =>
        [.. assembly.GetTypes()
            .Where(type => type is { IsAbstract: true, IsSealed: true } && type.Name.EndsWith("Handler", StringComparison.Ordinal))
            .Select(type => (type, Field: type.GetField("Type", BindingFlags.Public | BindingFlags.Static)))
            .Where(found => found.Field is { IsLiteral: true })
            .Select(found => (found.type, (string)found.Field!.GetRawConstantValue()!))];

    // The texts the generated file has for a type in a language: the values it uses and the words of each closed list, or none.
    private static (IReadOnlySet<string> Placeholders, Func<string, IReadOnlyCollection<string>> Words)? TextsOf(string type, Language language) =>
        NotificationText.TypesOf(language).Contains(type)
            ? (NotificationText.PlaceholdersOf(language, type), value => NotificationText.WordsOf(language, value))
            : null;

    private static List<string> Gaps(
        IEnumerable<NotificationKind> kinds,
        IReadOnlyList<string> categories,
        IReadOnlyList<(Type Handler, string Type)> handlers,
        Func<string, Language, (IReadOnlySet<string> Placeholders, Func<string, IReadOnlyCollection<string>> Words)?> textsOf)
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

    private static IEnumerable<string> TextGaps(
        NotificationKind kind,
        Language language,
        (IReadOnlySet<string> Placeholders, Func<string, IReadOnlyCollection<string>> Words)? texts)
    {
        var tag = JsonEnum<Language>.NameOf(language);
        if (texts is not { } found)
        {
            return [$"{kind.Type}: no texts in {tag}"];
        }

        var declared = kind.Values.Select(value => value.Name).Order(StringComparer.Ordinal).ToList();
        var used = found.Placeholders.Order(StringComparer.Ordinal).ToList();
        var placeholderGaps = declared.SequenceEqual(used)
            ? []
            : new[] { $"{kind.Type}: the texts in {tag} use {string.Join(", ", declared)}? they use {string.Join(", ", used)}" };
        var wordGaps = kind.Values
            .Where(value => value.Kind == NotificationValueKind.Word)
            .SelectMany(value => (value.Words ?? []).Where(word => !found.Words(value.Name).Contains(word)).Select(word => $"{kind.Type}: no word {word} of {value.Name} in {tag}"));
        return placeholderGaps.Concat(wordGaps);
    }
}
