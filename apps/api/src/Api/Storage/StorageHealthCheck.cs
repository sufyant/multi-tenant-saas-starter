using System.Text;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SharedKernel;

namespace Api.Storage;

// The readiness check of the storage: one round trip to the bucket under health/, once per process (section 7, "File storage", rule 7).
// A failure throws, the health check service reports it unhealthy, and the next check tries again.
internal sealed class StorageHealthCheck(IFileStore files) : IHealthCheck
{
    private volatile bool _passed;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (_passed)
        {
            return HealthCheckResult.Healthy();
        }

        var key = FileKeys.HealthCheck(Guid.CreateVersion7());
        var written = Encoding.UTF8.GetBytes(key);
        await files.SaveAsync(key, new MemoryStream(written), "text/plain", cancellationToken);
        using var read = new MemoryStream();
        await using (var content = await files.ReadAsync(key, cancellationToken))
        {
            await content.CopyToAsync(read, cancellationToken);
        }

        await files.DeleteAsync(key, cancellationToken);
        if (!read.ToArray().AsSpan().SequenceEqual(written))
        {
            return HealthCheckResult.Unhealthy("The storage gave back another content than it was given.");
        }

        _passed = true;
        return HealthCheckResult.Healthy();
    }
}
