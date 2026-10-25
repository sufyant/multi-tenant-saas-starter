using System.Security.Claims;
using Api.Authorization;
using ControlPlane.Contracts;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using SharedKernel;
using Wolverine;

namespace Api.IntegrationTests.Fixtures;

public static class GreetHandler
{
    public static Result<Greeting> Handle(Greet command) => new Greeting($"Hello, {command.Name}");
}

public static class AcknowledgeHandler
{
    public static Result Handle(Acknowledge command) => Result.Success();
}

public static class FailHandler
{
    public static Result Handle(Fail command) => new Error(ErrorCode.Declare("test.failure"), "The test asked for a failure.", command.Type);
}

public static class ExplodeHandler
{
    public static Result Handle(Explode command) =>
        throw new InvalidOperationException("Host=internal-db;Password=hunter2");
}

// Wolverine only discovers public handlers, messages and validators.
public sealed record Greet(string Name);

public sealed record Greeting(string Text);

public sealed class GreetValidator : AbstractValidator<Greet>
{
    public GreetValidator() => RuleFor(command => command.Name).NotEmpty().WithErrorCode(nameof(FieldErrorCode.Required));
}

public sealed record Acknowledge;

public sealed record Fail(ErrorType Type);

public sealed record Explode;

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

    public static void MapPlatform(RouteGroupBuilder platform) =>
        platform.MapGet("/ping", () => Results.Ok()).RequireAuthorization(Permissions.PlatformTenantsCreate);

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

        ErrorEndpoints.Map(v1);
    }
}

// Requests that fail in each way the error tracker must tell apart (section 7, "Error tracking"). None needs a test handler, so a host
// on the committed handler code maps them too.
internal static partial class ErrorEndpoints
{
    public const string Failure = "The test endpoint failed.";

    public static void Map(RouteGroupBuilder v1)
    {
        v1.MapPost("/throws", Throw);
        v1.MapPost("/logged-throws", LogThenThrow);
        v1.MapPost("/invalid", ThrowInvalid);
        v1.MapPost("/waits", WaitAsync);
        v1.MapPost("/failed-saves", FailToSaveAsync);
    }

    // An unexpected exception: the exception handler answers 500.
    private static IResult Throw() => throw new InvalidOperationException(Failure);

    // Code that logs an exception at Error and lets it go on, as Wolverine does with a command that fails while a request waits.
    private static IResult LogThenThrow(ILoggerFactory loggers)
    {
        var exception = new InvalidOperationException(Failure);
        LogFailed(loggers.CreateLogger(typeof(ErrorEndpoints)), exception);
        throw exception;
    }

    // An invalid command, as Wolverine's FluentValidation middleware stops it: the exception handler answers 400.
    private static IResult ThrowInvalid() =>
        throw new ValidationException([new FluentValidation.Results.ValidationFailure("name", "Required") { ErrorCode = nameof(FieldErrorCode.Required) }]);

    // A request that runs until its client leaves.
    private static async Task<IResult> WaitAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.Infinite, cancellationToken);
        return Results.Ok();
    }

    // A save the database refuses: EF Core logs the database's exception at Error and throws its own around it.
    private static async Task<IResult> FailToSaveAsync(ProbeDbContext probes, CancellationToken cancellationToken)
    {
        probes.Probes.Add(new Probe { Value = null! });
        await probes.SaveChangesAsync(cancellationToken);
        return Results.Ok();
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "The test endpoint failed")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}
