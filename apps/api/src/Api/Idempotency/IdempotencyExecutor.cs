using System.Diagnostics;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Api.Persistence;
using Microsoft.EntityFrameworkCore;
using SharedKernel;
using Wolverine;

namespace Api.Idempotency;

internal sealed class IdempotencyExecutor(IHttpContextAccessor accessor, ApiDbContext api, IMessageBus bus, TimeProvider time) : IIdempotencyExecutor
{
    public const string SubjectHeader = "idempotency-subject";
    public const string KeyHeader = "idempotency-key";
    public const string LeaseHeader = "idempotency-lease";
    public const string TraceHeader = "idempotency-trace";

    public async Task<Result<T>> ExecuteAsync<T>(object command, string? tenantId, CancellationToken cancellationToken)
    {
        var context = accessor.HttpContext ?? throw new InvalidOperationException("Idempotency runs inside an HTTP request.");
        var request = context.Request;
        var subject = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("An idempotent operation requires a signed-in caller.");
        var key = request.Headers[IdempotencyMiddleware.Header].ToString();
        var fingerprint = await HashRequestAsync(request, cancellationToken);
        var route = ((RouteEndpoint)context.GetEndpoint()!).RoutePattern.RawText!;
        var now = time.GetUtcNow();
        var lease = Guid.NewGuid();
        var until = now.AddMinutes(1);

        while (true)
        {
            // PostgreSQL commits this single statement before Wolverine starts the business transaction.
            var inserted = await api.Database.ExecuteSqlAsync($"""
                INSERT INTO api.idempotency_keys (subject, key, method, route, request_hash, lease, locked_until, created_at)
                VALUES ({subject}, {key}, {request.Method}, {route}, {fingerprint}, {lease}, {until}, {now})
                ON CONFLICT (subject, key) DO NOTHING
                """, cancellationToken);
            if (inserted == 1)
            {
                break;
            }

            var existing = await api.IdempotencyKeys.AsNoTracking().SingleOrDefaultAsync(row => row.Subject == subject && row.Key == key, cancellationToken);
            if (existing is null)
            {
                // The competing request failed and released its claim between the insert and the read.
                continue;
            }

            if (existing.Method != request.Method || existing.Route != route || existing.RequestHash != fingerprint)
            {
                return Error.Unprocessable(ErrorCode.IdempotencyKeyReused, "This idempotency key was used for another request.");
            }

            if (existing.Result is { } saved)
            {
                var result = JsonSerializer.Deserialize<SavedCommandResult<T>>(saved)!;
                context.Items[IdempotencyMiddleware.TraceItem] = result.TraceId;
                context.Response.Headers["Idempotent-Replayed"] = "true";
                return result.ToResult();
            }

            var renewed = await api.Database.ExecuteSqlAsync($"""
                WITH available AS (
                    SELECT subject, key FROM api.idempotency_keys
                    WHERE subject = {subject} AND key = {key} AND result IS NULL AND locked_until <= {now}
                    FOR UPDATE SKIP LOCKED
                )
                UPDATE api.idempotency_keys AS stored SET lease = {lease}, locked_until = {until}
                FROM available WHERE stored.subject = available.subject AND stored.key = available.key
                """, cancellationToken);
            if (renewed == 1)
            {
                break;
            }

            return Error.Conflict(ErrorCode.IdempotencyKeyInProgress, "The first request with this idempotency key is still running.");
        }

        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
        context.Items[IdempotencyMiddleware.TraceItem] = traceId;
        var delivery = new DeliveryOptions
        {
            TenantId = tenantId,
            Headers = { [SubjectHeader] = subject, [KeyHeader] = key, [LeaseHeader] = lease.ToString(), [TraceHeader] = traceId },
        };
        try
        {
            var result = await bus.InvokeAsync<Result<T>>(command, delivery, cancellationToken);
            if (!result.IsSuccess && result.Error.Type == ErrorType.Validation)
            {
                await ReleaseAsync(subject, key, lease);
            }

            return result;
        }
        catch
        {
            await ReleaseAsync(subject, key, lease);
            throw;
        }
    }

    private static async Task<string> HashRequestAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        request.Body.Position = 0;
        using var sha256 = SHA256.Create();
        await using var hashing = new CryptoStream(Stream.Null, sha256, CryptoStreamMode.Write, leaveOpen: true);
        await request.Body.CopyToAsync(hashing, cancellationToken);
        await hashing.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(request.RouteValues.OrderBy(pair => pair.Key, StringComparer.Ordinal)), cancellationToken);
        await hashing.FlushFinalBlockAsync(cancellationToken);
        return Convert.ToHexStringLower(sha256.Hash!);
    }

    private Task<int> ReleaseAsync(string subject, string key, Guid lease) => api.Database.ExecuteSqlAsync($"""
        DELETE FROM api.idempotency_keys WHERE subject = {subject} AND key = {key} AND lease = {lease} AND result IS NULL
        """, CancellationToken.None);
}
