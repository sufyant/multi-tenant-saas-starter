using ControlPlane.Domain.TermsVersions;

namespace ControlPlane.Application.TermsVersions.GetMyTerms;

public static class GetMyTermsHandler
{
    public static Task<MyTermsView> HandleAsync(GetMyTermsQuery query, ITermsVersionQueries terms, TimeProvider time, CancellationToken cancellationToken) =>
        terms.GetMyTermsAsync(query.ActorId, TermsVersion.DayOf(time.GetUtcNow()), cancellationToken);
}
