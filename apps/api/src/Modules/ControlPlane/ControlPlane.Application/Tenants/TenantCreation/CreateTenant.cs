using ControlPlane.Application.Invitations;
using ControlPlane.Application.Ports;
using ControlPlane.Domain;
using ControlPlane.Domain.Invitations;
using ControlPlane.Domain.Roles;
using ControlPlane.Domain.Tenants;
using System.Text.RegularExpressions;
using FluentValidation;
using SharedKernel;
using Wolverine;
using Wolverine.Persistence;

namespace ControlPlane.Application.Tenants.TenantCreation;

/// <summary>
/// A system admin creates a tenant. It runs inside the tenant it creates, whose id the caller chose, so every step
/// of the saga runs in that tenant's transaction and hands the tenant on to the next one. The host's idempotency layer protects
/// the HTTP request (section 3). The command carries no request key. The owner's invitation email is in the language given, else in
/// the system admin's own.
/// </summary>
public sealed record CreateTenant(string AdminId, string Name, string Slug, string OwnerEmail, Language? Language);

public sealed partial class CreateTenantValidator : AbstractValidator<CreateTenant>
{
    public CreateTenantValidator()
    {
        // The rules state the name's longest length and the slug's lengths and shape, so the OpenAPI document holds them; the tenant
        // still checks both itself (Evans, Chapter 6). The tenant keeps the name without the spaces around it, so its length is counted
        // without them.
        RuleFor(command => command.Name)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithErrorCode(nameof(FieldErrorCode.Required))
            .MaximumTrimmedLength(Tenant.NameMaxLength).WithErrorCode(nameof(FieldErrorCode.TooLong));
        RuleFor(command => command.Slug)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithErrorCode(nameof(FieldErrorCode.Required))
            .Length(Tenant.SlugMinLength, Tenant.SlugMaxLength).WithErrorCode(nameof(FieldErrorCode.SlugInvalid))
            .Matches(Slug()).WithErrorCode(nameof(FieldErrorCode.SlugInvalid));
        RuleFor(command => command.OwnerEmail)
            .NotEmpty().WithErrorCode(nameof(FieldErrorCode.Required))
            .EmailAddress().WithErrorCode(nameof(FieldErrorCode.EmailInvalid))
            .MaximumLength(Invitation.EmailMaxLength).WithErrorCode(nameof(FieldErrorCode.TooLong));
    }

    // Parts of lowercase ASCII letters and digits, joined by single hyphens. The OpenAPI document writes it as the slug's pattern, so
    // .NET and ECMAScript must read it the same way: no \z and no lookbehind. A $ would not do: in .NET it also matches before a last
    // line break. (?![\s\S]), no character after, is the end of the text in both.
    [GeneratedRegex(@"^[a-z0-9]+(-[a-z0-9]+)*(?![\s\S])", RegexOptions.CultureInvariant)]
    private static partial Regex Slug();
}

// Step 1 of the tenant creation saga, in one transaction: the tenant as provisioning, its first owner's invitation, the saga's record
// (section 6). The host also saves the command result in this transaction (I4). The invitation's secret is born here; only its hash is stored,
// and the accept link that carries it goes on in the activation's message. Every rejection returns before the handler changes
// anything, because Wolverine commits a failed Result too (W7); a taken slug is found by the insert itself, which writes nothing then.
public static class CreateTenantHandler
{
    public static async Task<(Result<TenantDetails>, Insert<TenantCreationSaga>?, DeliveryMessage<ActivateTenant>?, ActivationTimedOut?)> HandleAsync(
        CreateTenant command,
        ITenantCatalog catalog,
        InvitationSettings invitations,
        TenantCreationSettings tenantCreation,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var admin = await catalog.FindUserAsync(command.AdminId, cancellationToken)
            ?? throw new InvalidOperationException("A system admin is always a catalog user.");

        var now = time.GetUtcNow();
        var tenant = Tenant.Create(catalog.TenantId, command.Name, command.Slug, now);
        if (!tenant.IsSuccess)
        {
            return (tenant.Error, null, null, null);
        }

        if (!await catalog.TryAddAsync(tenant.Value, cancellationToken))
        {
            return (Error.Conflict(ControlPlaneErrorCodes.TenantSlugTaken, "Another tenant already has this slug."), null, null, null);
        }

        var secret = InvitationToken.Generate();
        var invitation = Invitation.Create(
            Guid.CreateVersion7(now),
            tenant.Value.Id,
            command.OwnerEmail,
            BuiltInRoles.Owner,
            admin.Id,
            now,
            invitations.Lifetime,
            secret,
            command.Language ?? admin.Language);
        catalog.Add(invitation);

        // R12: the new tenant's summary names its Owner invitation. The tenant was inserted by SQL, so it is read back, locked, to change it.
        var added = await catalog.FindTenantForUpdateAsync(cancellationToken)
            ?? throw new InvalidOperationException("The tenant was added in this transaction.");
        added.Summarize(await catalog.ReadRowsAsync(cancellationToken));

        var (saga, activate, timeout) = TenantCreationSaga.Begin(
            tenant.Value.Id,
            invitation.Id,
            invitations.AcceptLink(new InvitationCode(tenant.Value.Id, secret)),
            tenantCreation.Timeouts);

        // A saga that is only being started gives its messages no saga id of their own; this one needs it, so that its fault finds
        // the saga.
        return (
            TenantDetails.Of(tenant.Value),
            Storage.Insert(saga),
            activate.WithDeliveryOptions(new DeliveryOptions { SagaId = saga.Id.ToString() }),
            timeout);
    }
}
