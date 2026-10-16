using System.Security.Claims;
using Api.Authorization;
using ControlPlane.Contracts;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SharedKernel;
using Wolverine;

namespace Api.IntegrationTests.Fixtures;

internal static class TestEndpoints
{
    // A permission no built-in role holds: it is in no pool.
    public const string GuardedPermission = "test.guarded";

    // Stand-ins for host behaviour that is not about who the caller is, so they let anyone in.
    public static void Map(RouteGroupBuilder v1)
    {
        var open = v1.MapGroup("").AllowAnonymous();
        MapOpen(open);

        v1.MapGet("/whoami", (ClaimsPrincipal user) => Results.Ok(user.FindFirstValue(ClaimTypes.NameIdentifier))).RequireSignedIn();
        v1.MapPost("/retry-probes", (RetryProbe command, IMessageBus bus, CancellationToken cancellationToken) =>
            bus.InvokeAsync<Result>(command, cancellationToken)).RequireSignedIn();

        // An endpoint that states no access at all (A5).
        v1.MapGet("/unstated", () => Results.Ok());
    }

    private static void MapOpen(RouteGroupBuilder v1)
    {
        v1.MapPost("/greetings", (Greet command, IMessageBus bus, CancellationToken cancellationToken) =>
            bus.InvokeAsync<Result<Greeting>>(command, cancellationToken));

        v1.MapPost("/acknowledgements", (Acknowledge command, IMessageBus bus, CancellationToken cancellationToken) =>
            bus.InvokeAsync<Result>(command, cancellationToken));

        v1.MapPost("/failures/{type}", (ErrorType type, IMessageBus bus, CancellationToken cancellationToken) =>
            bus.InvokeAsync<Result>(new Fail(type), cancellationToken));

        v1.MapPost("/explosions", (IMessageBus bus, CancellationToken cancellationToken) =>
            bus.InvokeAsync<Result>(new Explode(), cancellationToken));

        v1.MapGet("/ping", () => Results.Ok());
    }

    // Tenant endpoints require a permission (A5); every built-in role holds this one, so any member gets in.
    public static void MapTenant(RouteGroupBuilder tenant)
    {
        tenant.MapGet("/ping", () => Results.Ok()).RequireAuthorization(Permissions.TenantMembersRead);

        tenant.MapPost("/probes", (WriteProbe command, IMessageBus bus, CancellationToken cancellationToken) =>
                bus.InvokeAsync<Result>(command, cancellationToken))
            .RequireAuthorization(Permissions.TenantMembersRead);

        tenant.MapPost("/failing-probes", (WriteProbeThenFail command, IMessageBus bus, CancellationToken cancellationToken) =>
                bus.InvokeAsync(command, cancellationToken))
            .RequireAuthorization(Permissions.TenantMembersRead);

        tenant.MapGet("/probes", (IMessageBus bus, CancellationToken cancellationToken) =>
                bus.InvokeAsync<Result<string[]>>(new ReadProbes(), cancellationToken))
            .RequireAuthorization(Permissions.TenantMembersRead);

        tenant.MapPost("/guarded", () => Results.Ok()).RequireAuthorization(GuardedPermission);
    }

    public static void MapAdmin(RouteGroupBuilder admin) =>
        admin.MapGet("/ping", () => Results.Ok()).RequireAuthorization(Permissions.AdminTenantsCreate);
}

// Wolverine only discovers public handlers, messages and validators.
public sealed record Greet(string Name);

public sealed record Greeting(string Text);

public sealed class GreetValidator : AbstractValidator<Greet>
{
    public GreetValidator() => RuleFor(command => command.Name).NotEmpty().WithErrorCode(nameof(FieldErrorCode.Required));
}

public static class GreetHandler
{
    public static Result<Greeting> Handle(Greet command) => new Greeting($"Hello, {command.Name}");
}

public sealed record Acknowledge;

public static class AcknowledgeHandler
{
    public static Result Handle(Acknowledge command) => Result.Success();
}

public sealed record Fail(ErrorType Type);

public static class FailHandler
{
    public static Result Handle(Fail command) => new Error(ErrorCode.Declare("test.failure"), "The test asked for a failure.", command.Type);
}

public sealed record Explode;

public static class ExplodeHandler
{
    public static Result Handle(Explode command) =>
        throw new InvalidOperationException("Host=internal-db;Password=hunter2");
}
