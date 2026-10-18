using ControlPlane.Application.Invitations;
using ControlPlane.Application.Invitations.SignUp;
using ControlPlane.Application.Ports;
using ControlPlane.Application.Tenants;
using ControlPlane.Application.Tenants.TenantCreation;
using ControlPlane.Contracts;
using ControlPlane.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharedKernel;
using Wolverine;
using Wolverine.Persistence;

namespace ControlPlane.IntegrationTests.Fixtures;

// The module's handlers, called in their tenant with the services Wolverine would pass them.
internal static class Handlers
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    // Tenant creation runs inside the tenant it creates, whose id the endpoint chooses. The saga it starts is inserted in the same
    // transaction, as Wolverine inserts a returned Insert<T>.
    public static Task<CreatedTenant> CreateTenantAsync(
        IServiceProvider services,
        Guid tenantId,
        string adminId,
        string slug,
        string ownerEmail) =>
        InTenant.RunAsync(services, tenantId, async scope =>
        {
            var (result, saga, activate, timeout) = await CreateTenantHandler.HandleAsync(
                new CreateTenantCommand(adminId, "Acme Ltd", slug, ownerEmail, null),
                scope.GetRequiredService<ITenantCatalog>(),
                scope.GetRequiredService<InvitationOptions>(),
                scope.GetRequiredService<TenantCreationOptions>(),
                scope.GetRequiredService<TimeProvider>(),
                Cancellation);
            if (saga is not null)
            {
                scope.GetRequiredService<ControlPlaneDbContext>().Add(saga.Entity);
            }

            return new CreatedTenant(result, saga, activate, timeout);
        });

    public static Task<(ActivateTenantReply? Completed, TenantActivatedEvent? Activated, OpenOwnerSignUpCommand? SignUp)> ActivateAsync(IServiceProvider services, Guid tenantId, ActivateTenantCommand step) =>
        InTenant.RunAsync(services, tenantId, scope => ActivateTenantHandler.HandleAsync(
            step,
            scope.GetRequiredService<ITenantCatalog>(),
            scope.GetRequiredService<TimeProvider>(),
            Cancellation));

    public static Task<CancelTenantReply?> CancelTenantAsync(IServiceProvider services, Guid tenantId, CancelTenantCommand step) =>
        InTenant.RunAsync(services, tenantId, scope => CancelTenantHandler.HandleAsync(
            step,
            scope.GetRequiredService<ITenantCatalog>(),
            scope.GetRequiredService<ILogger<TenantCreationSaga>>(),
            Cancellation));

    public static Task<WithdrawSignUpCommand?> CancelInvitationAsync(IServiceProvider services, Guid tenantId, CancelInvitationCommand step) =>
        InTenant.RunAsync(services, tenantId, scope => CancelInvitationHandler.HandleAsync(step, scope.GetRequiredService<ITenantCatalog>(), Cancellation));

    // The identity provider's answer for an invitation's sign-up, recorded in the invitation's tenant.
    public static Task<OutgoingMessages> RecordSignUpAsync(IServiceProvider services, Guid tenantId, RecordSignUpCommand opened) =>
        InTenant.RunAsync(services, tenantId, scope => RecordSignUpHandler.HandleAsync(
            opened,
            scope.GetRequiredService<ITenantCatalog>(),
            scope.GetRequiredService<TimeProvider>(),
            Cancellation));

    public static Task<WithdrawSignUpCommand?> ExpireSignUpAsync(IServiceProvider services, Guid tenantId, ExpireSignUpCommand expire) =>
        InTenant.RunAsync(services, tenantId, scope => ExpireSignUpHandler.HandleAsync(
            expire,
            scope.GetRequiredService<ITenantCatalog>(),
            scope.GetRequiredService<TimeProvider>(),
            Cancellation));

    // A tenant created up to its pivot, the way the saga drives it: started and activated. Returns what the owner's sign-up is opened
    // from.
    public static async Task<(Guid TenantId, string Slug, OpenOwnerSignUpCommand SignUp)> CreateActiveTenantAsync(IServiceProvider services, string ownerEmail)
    {
        var admin = await Catalog.AddUserAsync(services);
        var tenantId = Guid.CreateVersion7();
        var slug = Unique.Slug();
        var started = await CreateTenantAsync(services, tenantId, admin.ExternalId, slug, ownerEmail);
        var (_, _, signUp) = await ActivateAsync(services, tenantId, started.Activate.ShouldNotBeNull().Message);

        return (tenantId, slug, signUp.ShouldNotBeNull());
    }

    // Accepting starts outside any tenant: the invitation code names the tenant, the identity provider is asked for the user's
    // verified email addresses, and then the invitation is found by its secret in that tenant. A person the catalog does not know
    // gives their names.
    public static async Task<Result<MyTenantView>> AcceptAsync(
        IServiceProvider services,
        string code,
        string userId,
        string? firstName = "Ada",
        string? lastName = "Lovelace")
    {
        IReadOnlyList<string> verifiedEmails;
        await using (var scope = services.CreateAsyncScope())
        {
            verifiedEmails = await scope.ServiceProvider.GetRequiredService<IIdentityProvider>().FindVerifiedEmailsAsync(userId, Cancellation);
        }

        var (accepted, _) = await InTenant.RunAsync(services, TenantIdOf(code), scope => AcceptInvitationHandler.HandleAsync(
            new AcceptInvitationCommand(SecretOf(code), userId, verifiedEmails, firstName, lastName),
            scope.GetRequiredService<ITenantCatalog>(),
            scope.GetRequiredService<TimeProvider>(),
            Cancellation));
        return accepted;
    }

    // The invitation code an accept link carries: `<tenantId>.<secret>`.
    public static string CodeOf(Uri link) =>
        Uri.UnescapeDataString(link.Query.TrimStart('?').Split('&').Single(pair => pair.StartsWith("code=", StringComparison.Ordinal))["code=".Length..]);

    public static Guid TenantIdOf(string code) => Guid.Parse(code[..code.IndexOf('.', StringComparison.Ordinal)]);

    public static string SecretOf(string code) => code[(code.IndexOf('.', StringComparison.Ordinal) + 1)..];

    public sealed record CreatedTenant(
        Result<TenantView> Result,
        Insert<TenantCreationSaga>? Saga,
        DeliveryMessage<ActivateTenantCommand>? Activate,
        ActivationTimedOut? Timeout);
}
