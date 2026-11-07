using System.Reflection;
using Audit.Domain;
using ControlPlane.Contracts.Plans;
using Notifications.Application.Notifications;
using Notifications.Domain;
using Notifications.Domain.Notifications;
using SharedKernel;

namespace Api.ArchitectureTests;

// U1, U2: a notification type is a template. A type of the catalog has its channels, a footer in every language when it goes by email
// (U5), its texts in every language with exactly the values it declares, a text without each optional value, every word of a closed
// list in every language, and one handler that names it (its const Type). So a new type needs only its entry, its texts and its handler, and a type that misses one fails here. A planted catalog
// shows that each gap is found; a new entitlement key of ControlPlane fails here until the texts name it. A type's area has its word in every
// language, and its actor is a person of its values. U9: a type has the name of its event's audit operation, or is named apart with its reason.
public sealed class NotificationCatalogTests
{
    [Fact]
    public void DeclareNotificationTypes_TheCatalog_HasNoGap()
    {
        var gaps = Gaps(NotificationCatalog.Kinds.Values, HandlerTypes(typeof(NotificationText).Assembly), TextsOf);

        NotificationCatalog.Kinds.ShouldNotBeEmpty();
        gaps.ShouldBeEmpty();
    }

    [Fact]
    public void DeclareNotificationTypes_APlantedTypeWithoutTextsHandlerOrChannel_IsFound()
    {
        NotificationKind planted = new(
            "tenant.renamed",
            NotificationBell.Tenant,
            NotificationLinkKind.TenantSettings,
            "organisation_plan",
            [],
            [new("status", NotificationValueKind.Word, ["archived"]), new("actorName", NotificationValueKind.Person, Optional: true)]);

        var gaps = Gaps([planted], HandlerTypes(typeof(NotificationText).Assembly), TextsOf);

        gaps.ShouldBe(
        [
            "tenant.renamed: no channel",
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

        var gaps = Gaps([kind], HandlerTypes(typeof(NotificationText).Assembly), TextsOf);

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

        var gaps = Gaps([kind], HandlerTypes(typeof(NotificationText).Assembly), TextsOf);

        gaps.ShouldBe(
        [
            "tenant.suspended: no text without actorName in en",
            "tenant.suspended: no text without actorName in tr",
            "tenant.suspended: no text without actorName in ar",
        ]);
    }

    // The list names each item's area in the reader's language, so a type's area needs its word in every language. Its actor is one of
    // its people.
    [Fact]
    public void DeclareNotificationTypes_APlantedAreaWithoutItsWordsAndAnActorWhoIsNoPerson_IsFound()
    {
        var kind = NotificationCatalog.Of(NotificationCatalog.TenantPurged) with { Area = "billing", Actor = "tenantName" };

        var gaps = Gaps([kind], HandlerTypes(typeof(NotificationText).Assembly), TextsOf);

        gaps.ShouldBe(
        [
            "tenant.purged: the actor tenantName is no person of its values",
            "tenant.purged: no word billing of area in en",
            "tenant.purged: no word billing of area in tr",
            "tenant.purged: no word billing of area in ar",
        ]);
    }

    // The area and the actor of each type, as the list shows them: a member's news is Members, a change of the reader's own role or
    // membership Your access, the plan and its values Organisation plan, the platform's news Organisations, and the reader's own account,
    // such as a ready data export, Your account, with no actor: the worker did it. The actor is the person who acted: the one who changed or removed, the one who accepted an invitation, the system admin who changed a status.
    [Fact]
    public void DeclareNotificationTypes_TheAreasAndActors_FollowWhatEachTypeTellsOf()
    {
        var areas = NotificationCatalog.Kinds.Values.Select(kind => $"{kind.Type} {kind.Area} {kind.Actor}".TrimEnd());

        areas.ShouldBe(
        [
            "member.role_changed your_access actorName",
            "member.removed your_access actorName",
            "member.joined members memberName",
            "tenant.plan_changed organisation_plan",
            "tenant.entitlement_override_set organisation_plan",
            "tenant.entitlement_override_removed organisation_plan",
            "tenant.owner_joined organisations ownerName",
            "tenant.suspended organisations actorName",
            "tenant.reactivated organisations actorName",
            "tenant.closed organisations actorName",
            "tenant.reopened organisations actorName",
            "tenant.purged organisations",
            "tenant.suspended_for_owners your_access",
            "tenant.reactivated_for_owners your_access",
            "tenant.closed_for_owners your_access",
            "tenant.reopened_for_owners your_access",
            "tenant.purged_for_owners your_access",
            "user.data_export_ready your_account",
        ]);
    }

    // U5: an email says why it came, so a type of the email needs its footer in every language.
    [Fact]
    public void DeclareNotificationTypes_APlantedTypeOfTheEmailWithoutAFooter_IsFound()
    {
        var kind = NotificationCatalog.Of(NotificationCatalog.MemberJoined) with
        {
            Channels = [NotificationChannel.Web, NotificationChannel.Email],
        };

        var gaps = Gaps([kind], HandlerTypes(typeof(NotificationText).Assembly), TextsOf);

        gaps.ShouldBe(
        [
            "member.joined: no footer in en",
            "member.joined: no footer in tr",
            "member.joined: no footer in ar",
        ]);
    }

    // U5: a footer may name a value of its type, and only one the type always has, so no email goes out with a hole.
    [Fact]
    public void DeclareNotificationTypes_APlantedFooterWithAnOptionalValue_IsFound()
    {
        // The tenant is the first value of a changed role.
        var kind = NotificationCatalog.Of(NotificationCatalog.MemberRoleChanged);
        var planted = kind with { Values = [kind.Values[0] with { Optional = true }, .. kind.Values.Skip(1)] };

        var gaps = Gaps([planted], HandlerTypes(typeof(NotificationText).Assembly), TextsOf);

        gaps.ShouldContain("member.role_changed: the footer in en uses tenantName, which is no value the type always has");
    }

    // U4, U5: the product fixes each type's channels in the catalog, and delivery reads them from there only (the maintainer's decision of
    // 2026-11-01). When a person's access or the organisation's status changes, the email goes too: that person may not be able to open
    // the app and see the bell. Team news goes to the bell only.
    [Fact]
    public void DeclareNotificationTypes_TheChannels_AreTheEmailForAChangeOfAccessOrStatusAndTheWebOnlyForTeamNews()
    {
        var channels = NotificationCatalog.Kinds.Values.Select(kind => $"{kind.Type} {string.Join(" ", kind.Channels)}");

        channels.ShouldBe(
        [
            "member.role_changed Web Email",
            "member.removed Web Email",
            "member.joined Web",
            "tenant.plan_changed Web Email",
            "tenant.entitlement_override_set Web",
            "tenant.entitlement_override_removed Web",
            "tenant.owner_joined Web",
            "tenant.suspended Web Email",
            "tenant.reactivated Web Email",
            "tenant.closed Web Email",
            "tenant.reopened Web Email",
            "tenant.purged Web Email",
            "tenant.suspended_for_owners Web Email",
            "tenant.reactivated_for_owners Web Email",
            "tenant.closed_for_owners Web Email",
            "tenant.reopened_for_owners Web Email",
            "tenant.purged_for_owners Web Email",
            "user.data_export_ready Web Email",
        ]);
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
            ? new Texts(
                without => NotificationText.PlaceholdersOf(language, type, without),
                list => NotificationText.WordsOf(language, list),
                NotificationText.HasFooter(language, type),
                NotificationText.FooterPlaceholdersOf(language, type))
            : null;

    private static List<string> Gaps(
        IEnumerable<NotificationKind> kinds,
        IReadOnlyList<(Type Handler, string Type)> handlers,
        Func<string, Language, Texts?> textsOf)
    {
        List<string> gaps = [];
        foreach (var kind in kinds)
        {
            if (kind.Channels.Count == 0)
            {
                gaps.Add($"{kind.Type}: no channel");
            }

            if (kind.Actor is { } actor && !kind.Values.Any(value => value.Name == actor && value.Kind == NotificationValueKind.Person))
            {
                gaps.Add($"{kind.Type}: the actor {actor} is no person of its values");
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
        var footerGaps = kind.Channels.Contains(NotificationChannel.Email) && !found.HasFooter ? [$"{kind.Type}: no footer in {tag}"] : Array.Empty<string>();
        var always = kind.Values.Where(value => !value.Optional).Select(value => value.Name).ToHashSet(StringComparer.Ordinal);
        var footerValueGaps = found.FooterPlaceholders
            .Where(name => !always.Contains(name))
            .Select(name => $"{kind.Type}: the footer in {tag} uses {name}, which is no value the type always has");
        var areaGaps = found.Words(NotificationText.AreaWords).Contains(kind.Area) ? [] : new[] { $"{kind.Type}: no word {kind.Area} of area in {tag}" };
        return placeholderGaps.Concat(withoutGaps).Concat(wordGaps).Concat(footerGaps).Concat(footerValueGaps).Concat(areaGaps).Distinct();
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

    private sealed record Texts(
        Func<string?, IReadOnlySet<string>?> Placeholders,
        Func<string, IReadOnlyCollection<string>> Words,
        bool HasFooter,
        IReadOnlySet<string> FooterPlaceholders);
}
