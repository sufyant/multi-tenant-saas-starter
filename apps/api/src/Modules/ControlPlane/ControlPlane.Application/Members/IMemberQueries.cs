using ControlPlane.Application.Members.ListMembers;
using ControlPlane.Domain.Roles;
using SharedKernel;

namespace ControlPlane.Application.Members;

/// <summary>
/// The active tenant's members for the queries: each method reads without tracking and answers a read model, built in the query (Q2).
/// Row level security keeps them to the tenant. It never loads a membership to change it; <see cref="Tenants.ITenantRepository"/> does.
/// </summary>
/// <remarks>Public because a public handler takes it (W9).</remarks>
public interface IMemberQueries
{
    /// <summary>
    /// A page of the active tenant's members of the role or every role, with the copy of their profile and whether the user was deleted,
    /// whose email address or name contains the search: by the sort field, then by user id. The search starts from the tenant's
    /// memberships, never from the users.
    /// </summary>
    internal Task<ListPage<MemberView>> ListMembersAsync(ListRequest<ListMembersSort> list, BuiltInRole? role, CancellationToken cancellationToken);
}
