using Audit.Application.Ports;
using Audit.Domain;
using Microsoft.EntityFrameworkCore;

namespace Audit.Infrastructure.Persistence;

/// <summary>The audit logs on the module's DbContext, in the transaction Wolverine began for the handler.</summary>
/// <remarks>Public only so that Wolverine's generated code can build it on the handler's own DbContext (W1, W9).</remarks>
public sealed class AuditLog(AuditDbContext audit) : IAuditLog
{
    async Task IAuditLog.RecordAsync(PlatformAuditEntry? platform, AuditEntry? tenant, CancellationToken cancellationToken)
    {
        var stored = tenant is not null
            ? await audit.Entries.AnyAsync(entry => entry.Id == tenant.Id, cancellationToken)
            : await audit.PlatformEntries.AnyAsync(entry => entry.Id == platform!.Id, cancellationToken);
        if (stored)
        {
            return;
        }

        if (platform is not null)
        {
            audit.PlatformEntries.Add(platform);
        }

        if (tenant is not null)
        {
            audit.Entries.Add(tenant);
        }

        await audit.SaveChangesAsync(cancellationToken);
    }
}
