using ControlPlane.Application.Ports;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Api.Webhooks;

/// <summary>The identity provider's events about its users, which keep the catalog's copy of their profile fresh.</summary>
public static class ClerkEvents
{
    public static void Map(RouteGroupBuilder webhooks) =>
        webhooks.MapPost("/clerk", ReceiveClerkEvent).WithMetadata(new SignatureChecked());

    // The signature is checked on the raw body before anything else reads it. Every event with a valid signature answers 204, an
    // ignored one too: the sender retries any other answer. The command runs before the answer, so a failure reaches the sender as
    // a failure, and the sender tries again.
    private static async Task<Result> ReceiveClerkEvent(
        HttpRequest request,
        IIdentityProviderWebhook webhook,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(request.Body);
        var payload = await reader.ReadToEndAsync(cancellationToken);

        var read = webhook.Read(payload, name => request.Headers[name].ToString());
        if (!read.IsSuccess)
        {
            return read.Error;
        }

        if (read.Value is { } command)
        {
            await bus.InvokeAsync(command, cancellationToken);
        }

        return Result.Success();
    }
}
