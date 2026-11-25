using SharedKernel;

namespace Projects.Application.Projects.GetMember;

/// <summary>The user id of the signed-in member, by the identity provider's user id.</summary>
[MessageName("get-project-member")]
public sealed record GetMemberQuery(string ActorId);
