using SharedKernel;

namespace ControlPlane.Application.Members.MakeOwner;

/// <summary>
/// A system admin makes a member of the tenant the platform route names an Owner, whether or not the tenant has one (section 6, "Owner recovery").
/// The user comes by our id, which the platform reads from the user's detail; the platform reads no member list of the tenant.
/// </summary>
[MessageName("make-owner")]
public sealed record MakeOwnerCommand(string ActorId, Guid UserId);
