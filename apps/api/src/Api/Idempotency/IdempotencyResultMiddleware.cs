using Microsoft.EntityFrameworkCore;
using System.Diagnostics.CodeAnalysis;
using SharedKernel;
using Wolverine;

namespace Api.Idempotency;

[SuppressMessage("Design", "CA1000", Justification = "Wolverine closes the result and DbContext types when it generates each handler.")]
public static class IdempotencyResultMiddleware<T, TContext> where TContext : DbContext
{
    public static async Task BeforeAsync(Envelope envelope, TContext database, CancellationToken cancellationToken)
    {
        if (!envelope.Headers.TryGetValue(IdempotencyExecutor.KeyHeader, out var key))
        {
            return;
        }

        var subject = envelope.Headers[IdempotencyExecutor.SubjectHeader];
        var lease = Guid.Parse(envelope.Headers[IdempotencyExecutor.LeaseHeader]!);
        var owned = await database.Database.SqlQuery<Guid>($"""
            SELECT lease AS "Value" FROM api.idempotency_keys
            WHERE subject = {subject} AND key = {key} AND lease = {lease} FOR UPDATE
            """).SingleOrDefaultAsync(cancellationToken);
        if (owned != lease)
        {
            throw new InvalidOperationException("The idempotency claim was lost before the handler started.");
        }
    }

    public static async Task AfterAsync(Result<T> result, Envelope envelope, TContext database, CancellationToken cancellationToken)
    {
        if (!envelope.Headers.TryGetValue(IdempotencyExecutor.KeyHeader, out var key)
            || (!result.IsSuccess && result.Error.Type == ErrorType.Validation))
        {
            return;
        }

        var subject = envelope.Headers[IdempotencyExecutor.SubjectHeader];
        var lease = Guid.Parse(envelope.Headers[IdempotencyExecutor.LeaseHeader]!);
        var saved = SavedCommandResult<T>.Serialize(result, envelope.Headers[IdempotencyExecutor.TraceHeader]!);
        var written = await database.Database.ExecuteSqlAsync($"""
            UPDATE api.idempotency_keys SET result = CAST({saved} AS jsonb), locked_until = NULL
            WHERE subject = {subject} AND key = {key} AND lease = {lease} AND result IS NULL
            """, cancellationToken);
        if (written != 1)
        {
            throw new InvalidOperationException("The idempotency claim was lost. The business transaction must roll back.");
        }
    }
}
