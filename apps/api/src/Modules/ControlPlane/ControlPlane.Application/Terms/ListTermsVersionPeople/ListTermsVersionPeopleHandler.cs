using ControlPlane.Domain.Terms;
using SharedKernel;

namespace ControlPlane.Application.Terms.ListTermsVersionPeople;

public static class ListTermsVersionPeopleHandler
{
    public static async Task<Result<CursorPage<TermsPersonView>>> HandleAsync(
        ListTermsVersionPeopleQuery query,
        ITermsQueries terms,
        CancellationToken cancellationToken) =>
        await terms.ListPeopleAsync(query.VersionId, query.Status, query.Page, cancellationToken) is { } page ? page : TermsVersion.NotFound;
}
