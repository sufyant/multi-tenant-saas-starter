using System.Text.Json;

namespace Api.ArchitectureTests;

// K5: every list uses one contract, read from the committed OpenAPI document. A List operation takes page, pageSize, sort and q, and
// only filters whose values are an enum; its sort is an enum; its answer holds exactly items, page, pageSize and total.
public sealed class ListContractTests : IAsyncLifetime
{
    private const string DocumentFile = "openapi/v1.json";

    private static readonly string[] ContractParameters = ["page", "pageSize", "sort", "q"];

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
        var missing = Lists().SelectMany(list => ContractParameters
            .Where(name => !QueryParametersOf(list.Operation).Any(parameter => parameter.Name == name))
            .Select(name => $"{list.Id}: no {name}"));

        Lists().ShouldNotBeEmpty();
        missing.ShouldBeEmpty();
    }

    [Fact]
    public void BuildOpenApiDocument_EveryOtherQueryParameterOfAList_IsAnEnumFilter()
    {
        var notEnums = Lists().SelectMany(list => QueryParametersOf(list.Operation)
            .Where(parameter => !ContractParameters.Contains(parameter.Name) && !IsEnum(parameter.Schema))
            .Select(parameter => $"{list.Id}: {parameter.Name}"));

        Lists().ShouldNotBeEmpty();
        notEnums.ShouldBeEmpty();
    }

    [Fact]
    public void BuildOpenApiDocument_SortOfEveryList_IsAnEnum()
    {
        var notEnums = Lists()
            .Where(list => !QueryParametersOf(list.Operation).Any(parameter => parameter.Name == "sort" && IsEnum(parameter.Schema)))
            .Select(list => list.Id);

        Lists().ShouldNotBeEmpty();
        notEnums.ShouldBeEmpty();
    }

    [Fact]
    public void BuildOpenApiDocument_AnswerOfEveryList_HoldsExactlyItemsPagePageSizeAndTotal()
    {
        var answers = Lists().Select(list => $"{list.Id}: {string.Join(", ", AnswerPropertiesOf(list.Operation).Order(StringComparer.Ordinal))}");

        answers.ShouldNotBeEmpty();
        answers.ShouldBe(Lists().Select(list => $"{list.Id}: items, page, pageSize, total"));
    }

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
