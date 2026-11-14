using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Api.Routing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;

namespace Api.IntegrationTests.Fixtures;

// The terms gate (docs/flows/terms-of-use.md, section 5): every me and tenant endpoint of the composed application, called as a person who
// must accept a version, and the exempt list of the flow. The gate refuses before the endpoint runs, so a call needs no valid body: any
// answer but the gate's refusal shows that the gate let it pass.
internal static partial class TermsGateRoutes
{
    public const string RefusalCode = "terms_version.acceptance_required";

    // The flow's exempt list, section 5: the terms, the person's own account and their data, and Accept invitation's own path.
    public static readonly string[] Exempt =
    [
        "GET /v1/me/",
        "PATCH /v1/me/",
        "PATCH /v1/me/preferences",
        "POST /v1/me/email-change",
        "POST /v1/me/email-change/confirm",
        "POST /v1/me/account-deletion",
        "POST /v1/me/account-deletion/confirm",
        "POST /v1/me/data-exports",
        "GET /v1/me/data-exports/latest",
        "POST /v1/me/data-exports/{exportId:guid}/download",
        "POST /v1/me/invitations/accept",
        "POST /v1/me/invitations/preview",
        "GET /v1/me/terms",
        "POST /v1/me/terms/versions/{versionId:guid}/accept",
        "POST /v1/me/terms/versions/{versionId:guid}/dismiss",
        "GET /v1/me/notifications/stream",
    ];

    public static IReadOnlyList<RouteEndpoint> MeAndTenantEndpoints(IServiceProvider services) =>
    [
        .. services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<MeEndpoint>() is not null || endpoint.Metadata.GetMetadata<TenantScopedEndpoint>() is not null),
    ];

    public static string[] MarkedExempt(IEnumerable<RouteEndpoint> endpoints) =>
        [.. endpoints.Where(endpoint => endpoint.Metadata.GetMetadata<TermsGateExempt>() is not null).Select(Describe).Order(StringComparer.Ordinal)];

    // Each endpoint the gate should refuse and did not, and each exempt endpoint it refused.
    public static async Task<(List<string> NotRefused, List<string> ExemptRefused)> CallEveryAsync(
        HttpClient client, Guid tenantId, IEnumerable<RouteEndpoint> endpoints)
    {
        var notRefused = new List<string>();
        var exemptRefused = new List<string>();
        foreach (var endpoint in endpoints)
        {
            var refused = await CodeOfAsync(client, endpoint, tenantId) == RefusalCode;
            var exempt = endpoint.Metadata.GetMetadata<TermsGateExempt>() is not null;
            if (exempt && refused)
            {
                exemptRefused.Add(Describe(endpoint));
            }

            if (!exempt && !refused)
            {
                notRefused.Add(Describe(endpoint));
            }
        }

        return (notRefused, exemptRefused);
    }

    public static string Describe(RouteEndpoint endpoint) =>
        $"{endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods.Single()} {endpoint.RoutePattern.RawText}";

    // The headers are enough: the live stream never ends its body.
    private static async Task<string?> CodeOfAsync(HttpClient client, RouteEndpoint endpoint, Guid tenantId)
    {
        var method = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods.Single();
        var path = Parameter().Replace(endpoint.RoutePattern.RawText!, match => match.Groups["name"].Value == RouteGroups.TenantIdParameter
            ? tenantId.ToString()
            : match.Groups["guid"].Success ? Guid.NewGuid().ToString() : "unknown");
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method is "POST" or "PATCH" or "PUT")
        {
            request.Content = JsonContent.Create(new { });
        }

        using var answer = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, TestContext.Current.CancellationToken);
        if (answer.Content.Headers.ContentType?.MediaType != "application/problem+json")
        {
            return null;
        }

        var problem = await answer.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        return problem.GetProperty("code").GetString();
    }

    [GeneratedRegex(@"\{(?<name>[A-Za-z]+)(?<guid>:guid)?\}")]
    private static partial Regex Parameter();
}
