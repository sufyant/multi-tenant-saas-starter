using SharedKernel;

namespace ControlPlane.Application.TermsVersions.ListTermsVersionPeople;

/// <summary>
/// The users who may accept the version and accepted it, or did not, a page after a cursor (K5's exception: the frames page this list
/// with Load more, by a cursor, as the logs).
/// </summary>
[MessageName("list-terms-version-people")]
public sealed record ListTermsVersionPeopleQuery(Guid VersionId, TermsPersonStatus Status, CursorRequest Page);
