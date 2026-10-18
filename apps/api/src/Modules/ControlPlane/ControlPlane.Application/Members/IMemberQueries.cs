using ControlPlane.Application.Members.ListMembers;
using ControlPlane.Domain.Roles;
using ControlPlane.Domain.Tenants;
using SharedKernel;

namespace ControlPlane.Application.Members;

/// <summary>
/// The active tenant's members for the queries, read without tracking. Row level security keeps them to the tenant. It never loads a
/// membership to change it; <see cref="Tenants.ITenantRepository"/> does.
/// </summary>
/// <remarks>Public only because Wolverine's generated code passes it to public handlers.</remarks>
public interface IMemberQueries
{
    internal Task<Membership?> FindMembershipAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// A page of the active tenant's members of the role or every role, with the copy of their profile and whether the user was deleted,
    /// whose email address or name contains the search: by the sort field, then by user id. The search starts from the tenant's
    /// memberships, never from the users.
    /// </summary>
    internal Task<ListPage<(Guid UserId, string? Email, string? FirstName, string? LastName, BuiltInRole Role, bool Deleted, DateTimeOffset JoinedAt)>> ListMembersAsync(
        ListRequest<ListMembersSort> list,
        BuiltInRole? role,
        CancellationToken cancellationToken);
}
