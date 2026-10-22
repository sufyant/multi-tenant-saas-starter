using ControlPlane.Application.Identity;
using ControlPlane.Application.Invitations;
using ControlPlane.Application.Invitations.AcceptInvitation;
using ControlPlane.Application.Invitations.ExpireSignUp;
using ControlPlane.Application.Invitations.OpenSignUp;
using ControlPlane.Application.Invitations.RecordSignUp;
using ControlPlane.Application.Invitations.WithdrawSignUp;
using ControlPlane.Application.Tenants;
using ControlPlane.Application.Tenants.CreateTenant;
using ControlPlane.Application.Users;
using ControlPlane.Contracts.Tenants;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;
using Wolverine;

namespace ControlPlane.IntegrationTests.Fixtures;

// The module's handlers, called in their tenant with the services Wolverine would pass them.
internal static class Handlers
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    // Tenant creation runs inside the tenant it creates, whose id the endpoint chooses, in one transaction.
    public static Task<CreatedTenant> CreateTenantAsync(
        IServiceProvider services,
        Guid tenantId,
        string actorId,
        string slug,
        string ownerEmail) =>
        InTenant.RunAsync(services, tenantId, async scope =>
        {
            var (result, messages) = await CreateTenantHandler.HandleAsync(
                new CreateTenantCommand(actorId, "Acme Ltd", slug, ownerEmail, null),
                scope.GetRequiredService<ITenantRepository>(),
                scope.GetRequiredService<IUserRepository>(),
                scope.GetRequiredService<InvitationOptions>(),
                scope.GetRequiredService<TimeProvider>(),
                Cancellation);

            return new CreatedTenant(result, messages.OfType<TenantCreatedEvent>().SingleOrDefault(), messages.OfType<OpenSignUpCommand>().SingleOrDefault());
        });

    // The identity provider's answer for an invitation's sign-up, recorded in the invitation's tenant.
    public static Task<OutgoingMessages> RecordSignUpAsync(IServiceProvider services, Guid tenantId, RecordSignUpCommand opened) =>
        InTenant.RunAsync(services, tenantId, scope => RecordSignUpHandler.HandleAsync(
            opened,
            scope.GetRequiredService<ITenantRepository>(),
            scope.GetRequiredService<TimeProvider>(),
            Cancellation));

    public static async Task<WithdrawSignUpCommand?> ExpireSignUpAsync(IServiceProvider services, Guid tenantId, ExpireSignUpCommand expire) =>
        (await InTenant.RunAsync(services, tenantId, scope => ExpireSignUpHandler.HandleAsync(
            expire,
            scope.GetRequiredService<ITenantRepository>(),
            scope.GetRequiredService<TimeProvider>(),
            Cancellation))).OfType<WithdrawSignUpCommand>().SingleOrDefault();

    // A tenant a new user created, with its Owner's invitation pending. Returns what the owner's sign-up is opened from.
    public static async Task<(Guid TenantId, string Slug, OpenSignUpCommand SignUp)> CreateActiveTenantAsync(IServiceProvider services, string ownerEmail)
    {
        var admin = await TestData.AddUserAsync(services);
        var tenantId = Guid.CreateVersion7();
        var slug = Unique.Slug();
        var created = await CreateTenantAsync(services, tenantId, admin.ExternalId, slug, ownerEmail);

        return (tenantId, slug, created.SignUp.ShouldNotBeNull());
    }

    // Accepting starts outside any tenant: the invitation code names the tenant, the identity provider is asked for the user's
    // verified email addresses, and then the invitation is found by its secret in that tenant. A person who is not a user yet
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
            new AcceptInvitationCommand(userId, SecretOf(code), verifiedEmails, firstName, lastName),
            scope.GetRequiredService<ITenantRepository>(),
            scope.GetRequiredService<IUserRepository>(),
            scope.GetRequiredService<TimeProvider>(),
            Cancellation));
        return accepted;
    }

    // The invitation code an accept link carries: `<tenantId>.<secret>`.
    public static string CodeOf(Uri link) =>
        Uri.UnescapeDataString(link.Query.TrimStart('?').Split('&').Single(pair => pair.StartsWith("code=", StringComparison.Ordinal))["code=".Length..]);

    public static Guid TenantIdOf(string code) => Guid.Parse(code[..code.IndexOf('.', StringComparison.Ordinal)]);

    public static string SecretOf(string code) => code[(code.IndexOf('.', StringComparison.Ordinal) + 1)..];

    public sealed record CreatedTenant(Result<TenantView> Result, TenantCreatedEvent? Created, OpenSignUpCommand? SignUp);
}
