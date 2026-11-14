using ControlPlane.Domain.Terms;

namespace ControlPlane.Application.Terms.GetMyTerms;

public static class GetMyTermsHandler
{
    public static Task<MyTermsView> HandleAsync(GetMyTermsQuery query, ITermsQueries terms, TimeProvider time, CancellationToken cancellationToken) =>
        terms.GetMyTermsAsync(query.ActorId, TermsVersion.DayOf(time.GetUtcNow()), cancellationToken);
}
