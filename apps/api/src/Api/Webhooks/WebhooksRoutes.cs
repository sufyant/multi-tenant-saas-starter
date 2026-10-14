using Microsoft.AspNetCore.Mvc;

namespace Api.Webhooks;

// An external system that calls the API, proven by a signature (N5). A webhook delivery carries no user, so the group asks for none;
// each endpoint checks its sender's signature and declares it (A5). The version group's limit applies, and without a user it is kept
// for each client address. The body is read whole before its signature is checked, so the group takes at most 1 MB (API4).
internal static class WebhooksRoutes
{
    public const long MaxBodySize = 1024 * 1024;

    public static RouteGroupBuilder MapWebhooks(this RouteGroupBuilder v1) =>
        v1.MapGroup("/webhooks").AllowAnonymous().WithMetadata(new RequestSizeLimitAttribute(MaxBodySize));
}
