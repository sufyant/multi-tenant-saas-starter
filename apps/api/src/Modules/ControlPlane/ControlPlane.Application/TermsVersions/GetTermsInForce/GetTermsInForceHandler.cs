using ControlPlane.Domain.TermsVersions;

namespace ControlPlane.Application.TermsVersions.GetTermsInForce;

public static class GetTermsInForceHandler
{
    public static Task<TermsInForceView?> HandleAsync(GetTermsInForceQuery query, ITermsVersionQueries terms, TimeProvider time, CancellationToken cancellationToken) =>
        terms.FindVersionInForceAsync(TermsVersion.DayOf(time.GetUtcNow()), cancellationToken);
}
