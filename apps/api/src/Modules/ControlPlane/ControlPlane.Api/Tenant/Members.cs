using System.Text.Json.Serialization;
using ControlPlane.Application.Members;
using ControlPlane.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Api.Tenant;

/// <summary>The members of the tenant in the route.</summary>
public static class Members
{
    public static void Map(RouteGroupBuilder tenant) =>
        tenant.MapGet("/members", ListMembers).RequireAuthorization(Permissions.TenantMembersRead);

    // The tenant comes from the route: the host has declared it, and row level security keeps the list to it.
    private static async Task<Result<ListPage<MemberResponse>>> ListMembers(
        int? page,
        int? pageSize,
        ListSort<ListMembersSort>? sort,
        string? q,
        ListFilter<MemberRole>? role,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        var list = ListRequest.Create(page, pageSize, sort, q);
        if (!list.IsSuccess)
        {
            return list.Error;
        }

        var filter = ListFilter.Read(role, nameof(role));
        if (!filter.IsSuccess)
        {
            return filter.Error;
        }

        var members = await bus.InvokeAsync<Result<ListPage<MemberSummary>>>(new ListMembers(list.Value, filter.Value?.ToString()), cancellationToken);
        return members.Map(found => found.Map(member => new MemberResponse(member.UserId, member.Email, member.FirstName, member.LastName, member.Role)));
    }
}

/// <summary>
/// A member of the tenant: the user's id, the copy of their email address and name, and their role, Owner, Admin or Member. The email
/// address and the names are empty when the identity provider deleted the user; the screen shows "Deleted user".
/// </summary>
public sealed record MemberResponse(Guid UserId, string? Email, string? FirstName, string? LastName, string Role);

/// <summary>
/// A built-in role: the values of the role of a member and of an invitation, the role filter of the member and the invitation lists,
/// and the role an invitation gives.
/// </summary>
[JsonConverter(typeof(MemberRoleConverter))]
public enum MemberRole
{
    [JsonStringEnumMemberName("Owner")]
    Owner,

    [JsonStringEnumMemberName("Admin")]
    Admin,

    [JsonStringEnumMemberName("Member")]
    Member,
}

/// <summary>
/// Reads a role by its name only. The default converter also takes a number, so <c>7</c> would bind to no role and <c>0</c> to Owner.
/// </summary>
public sealed class MemberRoleConverter() : JsonStringEnumConverter<MemberRole>(allowIntegerValues: false);
