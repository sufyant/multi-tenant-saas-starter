using ControlPlane.Domain.TermsVersions;

namespace ControlPlane.Application.TermsVersions.GetTermsOfUse;

public static class GetTermsOfUseHandler
{
    public static Task<TermsOfUseView> HandleAsync(GetTermsOfUseQuery query, ITermsVersionQueries terms, TimeProvider time, CancellationToken cancellationToken) =>
        terms.GetTermsOfUseAsync(TermsVersion.DayOf(time.GetUtcNow()), cancellationToken);
}
