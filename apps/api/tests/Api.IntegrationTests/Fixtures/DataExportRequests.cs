using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Api.IntegrationTests.Fixtures;

// A person's data export through the API, as the screen asks for it, and its scheduled steps delivered now, as Wolverine delivers a
// scheduled message when its moment comes.
internal static class DataExportRequests
{
    public const string Path = "/v1/me/data-exports";

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    // Asks for an export and waits until the worker made it ready; answers its id.
    public static async Task<Guid> ReadyAsync(ApiFactory api, string user)
    {
        var requested = await api.WaitingForMessagesAsync(() => RequestAsync(api, user));
        return (await requested.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("id").GetGuid();
    }

    public static Task<HttpResponseMessage> RequestAsync(ApiFactory api, string user)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, Path);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        return api.CreateClient(user).SendAsync(request, Cancellation);
    }

    public static async Task<JsonElement> LatestAsync(ApiFactory api, string user) =>
        await (await api.CreateClient(user).GetAsync($"{Path}/latest", Cancellation)).Content.ReadFromJsonAsync<JsonElement>(Cancellation);

    public static Task DeliverAsync(ApiFactory api, object message) =>
        api.WaitingForMessagesAsync(async () =>
        {
            using var scope = api.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IMessageBus>().InvokeAsync(message, Cancellation);
            return message;
        });
}
