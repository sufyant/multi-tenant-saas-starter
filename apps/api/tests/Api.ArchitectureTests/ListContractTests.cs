using System.Text.Json;

namespace Api.ArchitectureTests;

// K5: every list uses one contract, read from the committed OpenAPI document. A List operation takes page, pageSize, sort and q, and
// only filters whose values are an enum; its sort is an enum; its answer holds exactly items, page, pageSize and total. The audit logs
// are the one exception: a List operation with a cursor takes exactly cursor and limit, and the platform's log also the tenant it is
// about, and answers exactly items and nextCursor, and only those two operations may.
public sealed class ListContractTests : IAsyncLifetime
{
    private const string DocumentFile = "openapi/v1.json";

    private static readonly string[] ContractParameters = ["page", "pageSize", "sort", "q"];

    private static readonly string[] CursorParameters = ["cursor", "limit"];

    private static readonly string[] CursorLists = ["ListAuditEntries", "ListPlatformAuditEntries"];

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
    public void BuildOpenApiDocument_EveryOtherQueryParameterOfAList_IsAnEnumFilter()
    {
        var notEnums = PageLists().SelectMany(list => QueryParametersOf(list.Operation)
            .Where(parameter => !ContractParameters.Contains(parameter.Name) && !IsEnum(parameter.Schema))
            .Select(parameter => $"{list.Id}: {parameter.Name}"));

        PageLists().ShouldNotBeEmpty();
        notEnums.ShouldBeEmpty();
    }

    // A19 and N7: a filter takes the enum of its response, so it refers to that enum's schema, and a client has one type for both.
    [Fact]
    public void BuildOpenApiDocument_EveryEnumFilterOfAList_RefersToTheSchemaOfItsEnum()
    {
        var inline = Lists().SelectMany(list => QueryParametersOf(list.Operation)
            .Where(parameter => parameter.Name != "sort" && IsEnum(parameter.Schema) && !parameter.Schema.TryGetProperty("$ref", out _))
            .Select(parameter => $"{list.Id}: {parameter.Name}"));

        Lists().ShouldNotBeEmpty();
        inline.ShouldBeEmpty();
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
        answers.ShouldBe(PageLists().Select(list => $"{list.Id}: items, page, pageSize, total"));
    }

    [Fact]
    public void BuildOpenApiDocument_ListsWithACursor_AreTheAuditLogsOnly()
    {
        var withACursor = CursorListsOf().Select(list => list.Id);

        withACursor.ShouldBe(CursorLists, ignoreOrder: true);
    }

    // Step 20k: the platform's log filters by the tenant an entry is about, for the Activity tab of a tenant.
    [Fact]
    public void BuildOpenApiDocument_EveryListWithACursor_TakesExactlyCursorAndLimitAndItsFilters()
    {
        var parameters = CursorListsOf().Select(list => $"{list.Id}: {string.Join(", ", QueryParametersOf(list.Operation).Select(parameter => parameter.Name).Order(StringComparer.Ordinal))}");

        parameters.ShouldBe(["ListAuditEntries: cursor, limit", "ListPlatformAuditEntries: cursor, limit, tenantId"], ignoreOrder: true);
    }

    [Fact]
    public void BuildOpenApiDocument_AnswerOfEveryListWithACursor_HoldsExactlyItemsAndNextCursor()
    {
        var answers = CursorListsOf().Select(list => $"{list.Id}: {string.Join(", ", AnswerPropertiesOf(list.Operation).Order(StringComparer.Ordinal))}");

        answers.ShouldNotBeEmpty();
        answers.ShouldBe(CursorListsOf().Select(list => $"{list.Id}: items, nextCursor"));
    }

    // A list follows the page contract unless it takes a cursor.
    private IEnumerable<(string Id, JsonElement Operation)> PageLists() => Lists().Where(list => !TakesACursor(list.Operation));

    private IEnumerable<(string Id, JsonElement Operation)> CursorListsOf() => Lists().Where(list => TakesACursor(list.Operation));

    private static bool TakesACursor(JsonElement operation) =>
        QueryParametersOf(operation).Any(parameter => CursorParameters.Contains(parameter.Name));

    private IEnumerable<(string Id, JsonElement Operation)> Lists() =>
        _document.GetProperty("paths").EnumerateObject()
            .SelectMany(path => path.Value.EnumerateObject().Select(operation => operation.Value))
            .Where(operation => operation.TryGetProperty("operationId", out var id) && id.GetString()!.StartsWith("List", StringComparison.Ordinal))
            .Select(operation => (operation.GetProperty("operationId").GetString()!, operation));

    private static IEnumerable<(string Name, JsonElement Schema)> QueryParametersOf(JsonElement operation) =>
        operation.TryGetProperty("parameters", out var parameters)
            ? parameters.EnumerateArray()
                .Where(parameter => parameter.GetProperty("in").GetString() == "query")
                .Select(parameter => (parameter.GetProperty("name").GetString()!, parameter.GetProperty("schema")))
            : [];

    private bool IsEnum(JsonElement schema) => Resolve(schema).TryGetProperty("enum", out var values) && values.GetArrayLength() > 0;

    private IEnumerable<string> AnswerPropertiesOf(JsonElement operation) =>
        Resolve(operation.GetProperty("responses").GetProperty("200").GetProperty("content").GetProperty("application/json").GetProperty("schema"))
            .GetProperty("properties").EnumerateObject().Select(property => property.Name);

    // A schema the document names is a reference into components/schemas.
    private JsonElement Resolve(JsonElement schema) =>
        schema.TryGetProperty("$ref", out var reference)
            ? _document.GetProperty("components").GetProperty("schemas").GetProperty(reference.GetString()!.Split('/')[^1])
            : schema;
}
