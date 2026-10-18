using ControlPlane.Application.Ports;
using ControlPlane.Contracts;
using ControlPlane.Domain.Tenants;
using FluentValidation;
using SharedKernel;

namespace ControlPlane.Application.Tenants;

/// <summary>
/// A system admin gives a tenant a new name; the slug never changes (K4). Only the platform renames a tenant: a member cannot. It runs
/// inside that tenant, which the platform route names, with the tenant locked.
/// </summary>
public sealed record RenameTenantCommand(string AdminId, string Name);

public sealed class RenameTenantValidator : AbstractValidator<RenameTenantCommand>
{
    public RenameTenantValidator()
    {
        // The rule states the name's longest length, so the OpenAPI document holds it; the tenant still checks the name itself, as at
        // its creation (Evans, Chapter 6). The tenant keeps the name without the spaces around it, so its length is counted without them.
        RuleFor(command => command.Name)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithErrorCode(nameof(FieldErrorCode.Required))
            .MaximumTrimmedLength(Tenant.NameMaxLength).WithErrorCode(nameof(FieldErrorCode.TooLong));
    }
}

// The command sets a state (I1): the name the tenant has answers as it is, and nothing is announced. Every rejection returns before the
// handler changes anything, because Wolverine commits a failed Result too (W7).
public static class RenameTenantHandler
{
    public static async Task<(Result<TenantDetails>, TenantRenamedEvent?)> HandleAsync(
        RenameTenantCommand command,
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
        return (TenantDetails.Of(tenant), new TenantRenamedEvent(Guid.CreateVersion7(now), now, tenant.Id, admin.Id, oldName, tenant.Name));
    }
}
