using ControlPlane.Application.Ports;
using ControlPlane.Contracts;
using FluentValidation;
using SharedKernel;

namespace ControlPlane.Application.Tenants;

/// <summary>
/// A system admin gives a tenant a new name; the slug never changes (K4). Only the platform renames a tenant: a member cannot. It runs
/// inside that tenant, which the platform route names, with the tenant locked.
/// </summary>
public sealed record RenameTenant(string AdminId, string Name);

public sealed class RenameTenantValidator : AbstractValidator<RenameTenant>
{
    public RenameTenantValidator()
    {
        // The tenant checks the name itself, as at its creation; a request without one never reaches it.
        RuleFor(command => command.Name).NotNull().WithErrorCode(nameof(FieldErrorCode.Required));
    }
}

// The command sets a state (I1): the name the tenant has answers as it is, and nothing is announced. Every rejection returns before the
// handler changes anything, because Wolverine commits a failed Result too (W7).
public static class RenameTenantHandler
{
    public static async Task<(Result<TenantDetails>, TenantRenamed?)> HandleAsync(
        RenameTenant command,
        ITenantCatalog catalog,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await catalog.FindTenantForUpdateAsync(cancellationToken) is not { } tenant)
        {
            return (Errors.TenantNotFound, null);
        }

        var oldName = tenant.Name;
        var renamed = tenant.Rename(command.Name);
        if (!renamed.IsSuccess || !renamed.Value)
        {
            return (renamed.Map(_ => TenantDetails.Of(tenant)), null);
        }

        var admin = await catalog.FindUserAsync(command.AdminId, cancellationToken)
            ?? throw new InvalidOperationException("A system admin is always a catalog user.");
        await catalog.SaveChangesAsync(cancellationToken);

        var now = time.GetUtcNow();
        return (TenantDetails.Of(tenant), new TenantRenamed(Guid.CreateVersion7(now), now, tenant.Id, admin.Id, oldName, tenant.Name));
    }
}
