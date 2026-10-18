using ControlPlane.Application.Ports;
using ControlPlane.Contracts;
using ControlPlane.Domain.Tenants;
using FluentValidation;
using SharedKernel;

namespace ControlPlane.Application.Tenants;

/// <summary>
/// A system admin suspends a tenant, for a reason they write. It runs inside that tenant, which the platform route names, with the tenant
/// locked, so a suspension and a reactivation run one at a time.
/// </summary>
public sealed record SuspendTenant(string AdminId, string Reason);

public sealed class SuspendTenantValidator : AbstractValidator<SuspendTenant>
{
    public SuspendTenantValidator()
    {
        // The tenant keeps the reason without the spaces around it, so its length is counted without them.
        RuleFor(command => command.Reason)
            .Cascade(CascadeMode.Stop)
            .Must(reason => !string.IsNullOrWhiteSpace(reason)).WithErrorCode(nameof(FieldErrorCode.Required))
            .MaximumTrimmedLength(Tenant.SuspensionReasonMaxLength).WithErrorCode(nameof(FieldErrorCode.TooLong));
    }
}

// The command sets a state (I1): a tenant already suspended answers as it is, and nothing is announced again. Every rejection returns
// before the handler changes anything, because Wolverine commits a failed Result too (W7).
public static class SuspendTenantHandler
{
    public static async Task<(Result<TenantDetails>, TenantSuspended?)> HandleAsync(
        SuspendTenant command,
        ITenantCatalog catalog,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await catalog.FindTenantForUpdateAsync(cancellationToken) is not { } tenant)
        {
            return (Errors.TenantNotFound, null);
        }

        var suspended = tenant.Suspend(command.Reason);
        if (!suspended.IsSuccess || !suspended.Value)
        {
            return (suspended.Map(_ => TenantDetails.Of(tenant)), null);
        }

        var admin = await catalog.FindUserAsync(command.AdminId, cancellationToken)
            ?? throw new InvalidOperationException("A system admin is always a catalog user.");
        await catalog.SaveChangesAsync(cancellationToken);

        var now = time.GetUtcNow();
        return (TenantDetails.Of(tenant), new TenantSuspended(Guid.CreateVersion7(now), now, tenant.Id, admin.Id, tenant.Name, tenant.SuspensionReason));
    }
}
