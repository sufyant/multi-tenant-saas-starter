using ControlPlane.Domain;
using SharedKernel;

namespace ControlPlane.Application;

/// <summary>The errors that several features of ControlPlane answer, and its API layer too.</summary>
public static class Errors
{
    public static readonly Error InvitationCodeRequired = Error.Validation(ControlPlaneErrorCodes.InvitationCodeRequired, "The request has no invitation code.");

    public static readonly Error InvitationNotFound = Error.NotFound(ControlPlaneErrorCodes.InvitationNotFound, "The invitation was not found.");

    public static readonly Error MemberNotFound = Error.NotFound(ControlPlaneErrorCodes.MemberNotFound, "The member was not found.");

    public static readonly Error TenantNotFound = Error.NotFound(ControlPlaneErrorCodes.TenantNotFound, "The tenant was not found.");

    public static readonly Error UserNotFound = Error.NotFound(ControlPlaneErrorCodes.UserNotFound, "The user was not found.");

    public static readonly Error UserIsSystemAdmin = Error.Conflict(ControlPlaneErrorCodes.UserIsSystemAdmin, "A system admin cannot be suspended or deleted.");

    /// <summary>The user is the last Owner of these tenants: a tenant always has at least one Owner (K1).</summary>
    public static Error UserLastOwner(IReadOnlyList<Guid> tenantIds) =>
        Error.Conflict(ControlPlaneErrorCodes.UserLastOwner, "The user is the last Owner of a tenant.") with { TenantIds = tenantIds };
}
