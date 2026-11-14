using ControlPlane.Domain.TermsVersions;
using SharedKernel;

namespace ControlPlane.Application.TermsVersions.GetTermsVersion;

public static class GetTermsVersionHandler
{
    public static async Task<Result<TermsVersionDetailView>> HandleAsync(
        GetTermsVersionQuery query,
        ITermsVersionQueries terms,
        TimeProvider time,
        CancellationToken cancellationToken) =>
        await terms.FindVersionAsync(query.VersionId, TermsVersion.DayOf(time.GetUtcNow()), cancellationToken) is { } version ? version : TermsVersion.NotFound;
}
