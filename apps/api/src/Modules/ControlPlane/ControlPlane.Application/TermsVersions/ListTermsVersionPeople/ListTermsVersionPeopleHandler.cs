using ControlPlane.Domain.TermsVersions;
using SharedKernel;

namespace ControlPlane.Application.TermsVersions.ListTermsVersionPeople;

public static class ListTermsVersionPeopleHandler
{
    public static async Task<Result<CursorPage<TermsPersonView>>> HandleAsync(
        ListTermsVersionPeopleQuery query,
        ITermsVersionQueries terms,
        CancellationToken cancellationToken) =>
        await terms.ListPeopleAsync(query.VersionId, query.Status, query.Page, cancellationToken) is { } page ? page : TermsVersion.NotFound;
}
