using ControlPlane.Domain.Terms;

namespace ControlPlane.Application.Terms.GetTermsInForce;

public static class GetTermsInForceHandler
{
    public static Task<TermsInForceView?> HandleAsync(GetTermsInForceQuery query, ITermsQueries terms, TimeProvider time, CancellationToken cancellationToken) =>
        terms.FindVersionInForceAsync(TermsVersion.DayOf(time.GetUtcNow()), cancellationToken);
}
