using ControlPlane.Domain.Terms;
using SharedKernel;

namespace ControlPlane.Application.Terms.GetTermsVersion;

public static class GetTermsVersionHandler
{
    public static async Task<Result<TermsVersionDetailView>> HandleAsync(
        GetTermsVersionQuery query,
        ITermsQueries terms,
        TimeProvider time,
        CancellationToken cancellationToken) =>
        await terms.FindVersionAsync(query.VersionId, TermsVersion.DayOf(time.GetUtcNow()), cancellationToken) is { } version ? version : TermsVersion.NotFound;
}
