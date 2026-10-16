using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace Api.IntegrationTests.ErrorHandling;

public sealed class ProblemDetailsTests : IAsyncLifetime
{
    private PipelineHost _host = null!;
    private HttpClient _client = null!;

    public async ValueTask InitializeAsync()
    {
        _host = await PipelineHost.StartAsync();
        _client = _host.CreateClient();
    }

    public async ValueTask DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task AnswerResult_SuccessWithAValue_ReturnsTheValue()
    {
        var response = await _client.PostAsJsonAsync("/v1/greetings", new { name = "Ada" }, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var greeting = await response.Content.ReadFromJsonAsync<Greeting>(TestContext.Current.CancellationToken);
        greeting.ShouldBe(new Greeting("Hello, Ada"));
    }

    [Fact]
    public async Task AnswerResult_SuccessWithoutAValue_ReturnsNoContent()
    {
        var response = await _client.PostAsJsonAsync("/v1/acknowledgements", new { }, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Theory]
    [InlineData("Validation", HttpStatusCode.BadRequest)]
    [InlineData("NotFound", HttpStatusCode.NotFound)]
    [InlineData("Conflict", HttpStatusCode.Conflict)]
    [InlineData("Forbidden", HttpStatusCode.Forbidden)]
    [InlineData("Unprocessable", HttpStatusCode.UnprocessableEntity)]
    [InlineData("TooManyRequests", HttpStatusCode.TooManyRequests)]
    public async Task AnswerResult_ExpectedFailure_ReturnsProblemDetailsWithItsStatusAndCode(string errorType, HttpStatusCode status)
    {
        var response = await _client.PostAsync($"/v1/failures/{errorType}", null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(status);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        problem!.Status.ShouldBe((int)status);
        problem.Detail.ShouldBe("The test asked for a failure.");
        problem.Extensions["code"]!.ToString().ShouldBe("tenant.not_found");
        problem.Extensions.ShouldContainKey("traceId");
    }

    // A validation answer names each field as the request's JSON does, with codes for a screen to write its own text, never messages.
    [Fact]
    public async Task AnswerResult_InvalidCommand_ListsAFieldCodeForEachField()
    {
        var response = await _client.PostAsJsonAsync("/v1/greetings", new { name = "" }, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        problem.GetProperty("code").GetString().ShouldBe("request.validation_failed");
        problem.GetProperty("errors").GetRawText().ShouldBe("""{"name":["required"]}""");
    }

    // A request the endpoint cannot read at all carries a code too.
    [Fact]
    public async Task AnswerResult_UnreadableBody_IsAnInvalidRequest()
    {
        var response = await _client.PostAsync(
            "/v1/greetings", new StringContent("{", Encoding.UTF8, "application/json"), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await CodeOfAsync(response)).ShouldBe("request.invalid");
    }

    // Every error answer the host writes carries a code, also one no endpoint returned.
    [Theory]
    [InlineData("GET", "/v1/nowhere", "text/plain", HttpStatusCode.NotFound, "route.not_found")]
    [InlineData("DELETE", "/v1/greetings", "application/json", HttpStatusCode.MethodNotAllowed, "route.method_not_allowed")]
    [InlineData("POST", "/v1/greetings", "text/plain", HttpStatusCode.UnsupportedMediaType, "request.unsupported_media_type")]
    public async Task AnswerRequest_TheHostRefusesIt_AnswersProblemDetailsWithACode(string method, string path, string contentType, HttpStatusCode status, string code)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path) { Content = new StringContent("{}", Encoding.UTF8, contentType) };

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(status);
        (await CodeOfAsync(response)).ShouldBe(code);
    }

    [Fact]
    public async Task AnswerResult_UnexpectedException_ReturnsAGenericServerError()
    {
        var response = await _client.PostAsync("/v1/explosions", null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        problem!.Status.ShouldBe(500);
        problem.Extensions.ShouldContainKey("traceId");
        problem.Extensions["code"]!.ToString().ShouldBe("server.error");
    }

    [Fact]
    public async Task AnswerResult_UnexpectedException_LeaksNoMessageOrStackTrace()
    {
        var response = await _client.PostAsync("/v1/explosions", null, TestContext.Current.CancellationToken);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldNotContain("hunter2");
        body.ShouldNotContain(nameof(InvalidOperationException));
        body.ShouldNotContain("ExplodeHandler");
    }

    private static async Task<string?> CodeOfAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("code").GetString();
}
