using System.Text;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SharedKernel;

namespace Api.Storage;

// A process is ready only once it has used its bucket: it writes a small file under health/, reads it back and deletes it, through the
// same store as the modules. Railway sends no traffic to a new deployment until it is ready, and deploy.yml waits for that, so a deploy
// that cannot use its bucket fails with the API's own settings and no other secret (section 7, "File storage"). The check passes once per
// process and then only remembers it, because the worker's heartbeat runs the readiness checks every interval. A failure throws, the
// health check service reports it unhealthy, and the next check tries again. The bucket's lifecycle rule deletes a file that a failure
// left (.cloudflare/).
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
