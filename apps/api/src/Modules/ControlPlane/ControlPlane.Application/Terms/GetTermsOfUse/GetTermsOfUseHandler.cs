using ControlPlane.Domain.Terms;

namespace ControlPlane.Application.Terms.GetTermsOfUse;

public static class GetTermsOfUseHandler
{
    public static Task<TermsOfUseView> HandleAsync(GetTermsOfUseQuery query, ITermsQueries terms, TimeProvider time, CancellationToken cancellationToken) =>
        terms.GetTermsOfUseAsync(TermsVersion.DayOf(time.GetUtcNow()), cancellationToken);
}
