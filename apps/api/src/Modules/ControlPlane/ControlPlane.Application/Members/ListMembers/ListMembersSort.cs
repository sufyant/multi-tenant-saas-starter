namespace ControlPlane.Application.Members.ListMembers;

/// <summary>The fields the member list sorts by. The first is the default: Owner, Admin, Member, then the custom roles by name.</summary>
public enum ListMembersSort
{
    Role,
    Email,
    LastName,
}
