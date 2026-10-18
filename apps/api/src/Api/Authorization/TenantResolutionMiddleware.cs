using System.Diagnostics;
using System.Security.Claims;
using Api.Authentication;
using Api.Routing;
using ControlPlane.Contracts;
using Microsoft.AspNetCore.Http.Features;
using Serilog;
using Serilog.Context;
using Wolverine;

namespace Api.Authorization;

// Resolves the tenant of a request and what the caller may do there, never from the client's word alone. On a me or a tenant route
// ControlPlane first says whether the user may act at all: a suspended user is refused (A9). On a tenant route the tenant then comes
// from the id in the path and the user's membership, with the permissions of the member's role, or ControlPlane's reason to refuse:
// tenant.suspended to a member, tenant.not_found to anyone else (T3); on a platform route ControlPlane answers with the user's platform permissions, given whether the session verified a second
// factor (A6). It rejects nothing: authorization turns away the callers it finds no access for.
internal sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    private const string TenantTag = "tenant.id";

    public async Task InvokeAsync(
        HttpContext context,
        ITenantDirectory directory,
        ISystemAdminDirectory systemAdmins,
        IUserDirectory users,
        RequestAccess access,
        IMessageBus bus,
        IDiagnosticContext diagnostics)
    {
        var metadata = context.GetEndpoint()?.Metadata;
        if (metadata is null || context.User.FindFirstValue(ClaimTypes.NameIdentifier) is not { } userId)
        {
            await next(context);
            return;
        }

        var tenantScoped = metadata.GetMetadata<TenantScopedEndpoint>() is not null;
        if (metadata.GetMetadata<PlatformEndpoint>() is not null)
        {
            access.PlatformPermissions = await systemAdmins.FindPlatformPermissionsAsync(
                userId, context.User.HasVerifiedSecondFactor(), context.RequestAborted);
        }
        else if (tenantScoped || metadata.GetMetadata<MeEndpoint>() is not null)
        {
            var active = await users.CheckActiveAsync(userId, context.RequestAborted);
            access.Refusal = active.IsSuccess ? null : active.Error;
        }

        Guid? tenantId = null;
        if (tenantScoped
            && access.Refusal is null
            && Guid.TryParse(context.GetRouteValue(RouteGroups.TenantIdParameter) as string, out var requested))
        {
            var found = await directory.FindMembershipAsync(requested, userId, context.RequestAborted);
            access.Membership = found.IsSuccess ? found.Value : null;
            access.Refusal = found.IsSuccess ? null : found.Error;
            tenantId = access.Membership?.TenantId;
        }

        if (tenantId is not { } resolved)
        {
            await next(context);
            return;
        }

        // Commands sent from the request carry the tenant in their envelope, and their handlers' transactions declare it (W2, W3).
        bus.TenantId = resolved.ToString();

        // Every signal carries the tenant.
        var id = resolved.ToString();
        diagnostics.Set("TenantId", id);
        Activity.Current?.SetTag(TenantTag, id);
        context.Features.Get<IHttpMetricsTagsFeature>()?.Tags.Add(new(TenantTag, id));
        using (LogContext.PushProperty("TenantId", id))
        {
            await next(context);
        }
    }
}
