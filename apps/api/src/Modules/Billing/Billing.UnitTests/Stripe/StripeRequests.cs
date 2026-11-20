using System.Net;
using System.Text;

namespace Billing.UnitTests.Stripe;

// Stripe's API for the adapter's tests, as a handler of HttpClient: each request is kept, method, path, form body and idempotency key, and answered with the
// first canned answer whose method and path start match, else 404. No call leaves the process.
internal sealed class StripeRequests : HttpMessageHandler
{
    private static readonly Canned NotCanned = new(string.Empty, string.Empty, HttpStatusCode.NotFound, """{"error": {"type": "invalid_request_error", "message": "not canned"}}""");

    private readonly List<Canned> _answers = [];

    public List<(string Method, string Path, string Body, string? IdempotencyKey)> Sent { get; } = [];

    public StripeRequests Answer(string method, string path, string json, HttpStatusCode status = HttpStatusCode.OK)
    {
        _answers.Add(new Canned(method, path, status, json));
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : WebUtility.UrlDecode(await request.Content.ReadAsStringAsync(cancellationToken));
        var path = request.RequestUri!.PathAndQuery;
        Sent.Add((request.Method.Method, path, body, request.Headers.TryGetValues("Idempotency-Key", out var keys) ? keys.Single() : null));
        var answer = _answers.FirstOrDefault(canned => canned.Method == request.Method.Method && path.StartsWith(canned.Path, StringComparison.Ordinal)) ?? NotCanned;
        return new HttpResponseMessage(answer.Status) { Content = new StringContent(answer.Json, Encoding.UTF8, "application/json") };
    }

    private sealed record Canned(string Method, string Path, HttpStatusCode Status, string Json);
}
