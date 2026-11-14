using System.Text.Json;

namespace Api.ArchitectureTests;

// K5: every list uses one contract, read from the committed OpenAPI document. A List operation takes page, pageSize, sort and q, and
// only filters whose values are an enum, an id (a uuid, such as the role filter of the member and the invitation lists, step r4b), a
// boolean field of the response (isSystemAdmin of the user list, step r12) or an email address that this test names (email of the user
// list, step r13), so no list takes a free-text filter by accident; its sort is an enum; its answer holds exactly items, page, pageSize and total.
// The platform's tenant and user lists are named apart: their status filter takes several values, and their answer counts each status
// (step 37b). The audit logs
// are the one exception: a List operation with a cursor takes exactly cursor and limit, and the platform's log also the tenant it is
// about, and answers exactly items and nextCursor, and only those two operations may.
public sealed class ListContractTests : IAsyncLifetime
{
    private const string DocumentFile = "openapi/v1.json";

    private static readonly string[] ContractParameters = ["page", "pageSize", "sort", "q"];

    private static readonly string[] CursorParameters = ["cursor", "limit"];

    // K5: the audit logs and the bells (U7) read newest first by a cursor, and so do a terms version's people, whose frames page with Load
    // more (step 38).
    private static readonly string[] CursorLists =
        ["ListAuditEntries", "ListPlatformAuditEntries", "ListNotifications", "ListMyNotifications", "ListPlatformNotifications", "ListTermsVersionPeople"];

    // Step r13: the System admins "Add" dialog finds a person by the whole address. An email filter is plain equality on the address in
    // its one form, never a search; q searches.
    private static readonly string[] EmailFilters = ["ListUsers: email"];

    // Step 37b: the tabs of the platform's Organisations and Users lists. Each tab is one status or several, and shows its count.
    private static readonly string[] MultiValueFilters = ["ListTenants: status", "ListUsers: status"];

    private static readonly string[] StatusCountedLists = ["ListTenants", "ListUsers"];

    private JsonElement _document;

    public ValueTask InitializeAsync()
    {
        _document = JsonDocument.Parse(File.ReadAllText(Path.Combine(Solution.Root, DocumentFile))).RootElement;
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public void BuildOpenApiDocument_EveryList_TakesPagingSortAndSearch()
    {
        var missing = PageLists().SelectMany(list => ContractParameters
            .Where(name => !QueryParametersOf(list.Operation).Any(parameter => parameter.Name == name))
            .Select(name => $"{list.Id}: no {name}"));

        PageLists().ShouldNotBeEmpty();
        missing.ShouldBeEmpty();
    }

    [Fact]
    public void BuildOpenApiDocument_EveryOtherQueryParameterOfAList_IsAnEnumAnIdABooleanOrAnEmailFilter()
    {
        var notEnums = PageLists().SelectMany(list => QueryParametersOf(list.Operation)
            .Where(parameter => !ContractParameters.Contains(parameter.Name)
                && !IsEnum(parameter.Schema)
                && !IsEnumArray(parameter.Schema)
                && !IsId(parameter.Schema)
                && !IsBoolean(parameter.Schema)
                && !IsEmail(parameter.Schema))
            .Select(parameter => $"{list.Id}: {parameter.Name}"));

        PageLists().ShouldNotBeEmpty();
        notEnums.ShouldBeEmpty();
    }

    // N7h and N11e: a filter takes the enum of its response, so it refers to that enum's schema, and a client has one type for both.
    [Fact]
    public void BuildOpenApiDocument_EveryEnumFilterOfAList_RefersToTheSchemaOfItsEnum()
    {
        var inline = Lists().SelectMany(list => QueryParametersOf(list.Operation)
            .Where(parameter => parameter.Name != "sort" && IsEnum(parameter.Schema) && !parameter.Schema.TryGetProperty("$ref", out _))
            .Select(parameter => $"{list.Id}: {parameter.Name}"));

        Lists().ShouldNotBeEmpty();
        inline.ShouldBeEmpty();
    }

    // Step 37b: a filter of several values is an array of its enum, sent as the parameter once for each value, and only the named
    // filters are one; every other filter takes one value.
    [Fact]
    public void BuildOpenApiDocument_FiltersOfSeveralValues_AreTheNamedOnesOnlyAndEachAnArrayOfItsEnum()
    {
        var arrays = Lists().SelectMany(list => QueryParametersOf(list.Operation)
            .Where(parameter => parameter.Schema.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String && type.GetString() == "array")
            .Select(parameter => $"{list.Id}: {parameter.Name}"));
        var notReferences = Lists().SelectMany(list => QueryParametersOf(list.Operation)
            .Where(parameter => IsEnumArray(parameter.Schema) && !parameter.Schema.GetProperty("items").TryGetProperty("$ref", out _))
            .Select(parameter => $"{list.Id}: {parameter.Name}"));

        arrays.ShouldBe(MultiValueFilters, ignoreOrder: true);
        notReferences.ShouldBeEmpty();
    }

    // Step r13: an email filter is a kind of its own, so each one is named here, and a new one fails this test until someone decides it.
    [Fact]
    public void BuildOpenApiDocument_EmailFiltersOfTheLists_AreTheNamedOnesOnly()
    {
        var emailFilters = Lists().SelectMany(list => QueryParametersOf(list.Operation)
            .Where(parameter => IsEmail(parameter.Schema))
            .Select(parameter => $"{list.Id}: {parameter.Name}"));

        emailFilters.ShouldBe(EmailFilters, ignoreOrder: true);
    }

    [Fact]
    public void BuildOpenApiDocument_SortOfEveryList_IsAnEnum()
    {
        var notEnums = PageLists()
            .Where(list => !QueryParametersOf(list.Operation).Any(parameter => parameter.Name == "sort" && IsEnum(parameter.Schema)))
            .Select(list => list.Id);

        PageLists().ShouldNotBeEmpty();
        notEnums.ShouldBeEmpty();
    }

    [Fact]
    public void BuildOpenApiDocument_AnswerOfEveryList_HoldsExactlyItemsPagePageSizeAndTotal()
    {
        var answers = PageLists().Select(list => $"{list.Id}: {string.Join(", ", AnswerPropertiesOf(list.Operation).Order(StringComparer.Ordinal))}");

        answers.ShouldNotBeEmpty();
        answers.ShouldBe(PageLists().Select(list => ExpectedAnswerOf(list.Id)));
    }

    [Fact]
    public void BuildOpenApiDocument_ListsWithACursor_AreTheAuditLogsAndTheBellsOnly()
    {
        var withACursor = CursorListsOf().Select(list => list.Id);

        withACursor.ShouldBe(CursorLists, ignoreOrder: true);
    }

    // Step 20k: the platform's log filters by the tenant an entry is about, for the Activity tab of a tenant. Step 35: each bell filters its
    // unread ones, for its Unread tab.
    [Fact]
    public void BuildOpenApiDocument_EveryListWithACursor_TakesExactlyCursorAndLimitAndItsFilters()
    {
        var parameters = CursorListsOf().Select(list => $"{list.Id}: {string.Join(", ", QueryParametersOf(list.Operation).Select(parameter => parameter.Name).Order(StringComparer.Ordinal))}");

        parameters.ShouldBe(
            [
                "ListAuditEntries: cursor, limit",
                "ListPlatformAuditEntries: cursor, limit, tenantId",
                "ListNotifications: cursor, limit, unread",
                "ListMyNotifications: cursor, limit, unread",
                "ListPlatformNotifications: cursor, limit, unread",
                "ListTermsVersionPeople: cursor, limit, status",
            ],
            ignoreOrder: true);
    }

    [Fact]
    public void BuildOpenApiDocument_AnswerOfEveryListWithACursor_HoldsExactlyItemsAndNextCursor()
    {
        var answers = CursorListsOf().Select(list => $"{list.Id}: {string.Join(", ", AnswerPropertiesOf(list.Operation).Order(StringComparer.Ordinal))}");

        answers.ShouldNotBeEmpty();
        answers.ShouldBe(CursorListsOf().Select(list => $"{list.Id}: items, nextCursor"));
    }

    private static string ExpectedAnswerOf(string list) =>
        StatusCountedLists.Contains(list) ? $"{list}: items, page, pageSize, statusCounts, total" : $"{list}: items, page, pageSize, total";

    // An id filter takes one id, which the same field of the response holds (roleId: role.id).
    private static bool IsId(JsonElement schema) =>
        schema.TryGetProperty("format", out var format) && format.GetString() == "uuid";

    private static bool IsBoolean(JsonElement schema) =>
        schema.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String && type.GetString() == "boolean";

    // An email filter is a text in the format email; any other text is no filter a list may take.
    private static bool IsEmail(JsonElement schema) =>
        schema.TryGetProperty("format", out var format) && format.GetString() == "email";

    private static bool TakesACursor(JsonElement operation) =>
        QueryParametersOf(operation).Any(parameter => CursorParameters.Contains(parameter.Name));

    private static IEnumerable<(string Name, JsonElement Schema)> QueryParametersOf(JsonElement operation) =>
        operation.TryGetProperty("parameters", out var parameters)
            ? parameters.EnumerateArray()
                .Where(parameter => parameter.GetProperty("in").GetString() == "query")
                .Select(parameter => (parameter.GetProperty("name").GetString()!, parameter.GetProperty("schema")))
            : [];

    // A list follows the page contract unless it takes a cursor.
    private IEnumerable<(string Id, JsonElement Operation)> PageLists() => Lists().Where(list => !TakesACursor(list.Operation));

    private IEnumerable<(string Id, JsonElement Operation)> CursorListsOf() => Lists().Where(list => TakesACursor(list.Operation));

    private IEnumerable<(string Id, JsonElement Operation)> Lists() =>
        _document.GetProperty("paths").EnumerateObject()
            .SelectMany(path => path.Value.EnumerateObject().Select(operation => operation.Value))
            .Where(operation => operation.TryGetProperty("operationId", out var id) && id.GetString()!.StartsWith("List", StringComparison.Ordinal))
            .Select(operation => (operation.GetProperty("operationId").GetString()!, operation));

    private bool IsEnum(JsonElement schema) => Resolve(schema).TryGetProperty("enum", out var values) && values.GetArrayLength() > 0;

    private bool IsEnumArray(JsonElement schema) =>
        schema.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String && type.GetString() == "array"
        && schema.TryGetProperty("items", out var items) && IsEnum(items);

    private IEnumerable<string> AnswerPropertiesOf(JsonElement operation) =>
        Resolve(operation.GetProperty("responses").GetProperty("200").GetProperty("content").GetProperty("application/json").GetProperty("schema"))
            .GetProperty("properties").EnumerateObject().Select(property => property.Name);

    // A schema the document names is a reference into components/schemas.
    private JsonElement Resolve(JsonElement schema) =>
        schema.TryGetProperty("$ref", out var reference)
            ? _document.GetProperty("components").GetProperty("schemas").GetProperty(reference.GetString()!.Split('/')[^1])
            : schema;
}
