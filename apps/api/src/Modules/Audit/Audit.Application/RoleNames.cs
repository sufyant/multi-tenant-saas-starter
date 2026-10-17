namespace Audit.Application;

// The name of a built-in role in an audit record: owner, admin or member, as ControlPlane's events and the API name it (catalog, K5).
// A message written before step 20k names it as ControlPlane's domain does, Owner. Such a message may still wait in the outbox during the
// deploy, and its record names the role as every other record does.
internal static class RoleNames
{
    public static string Of(string role) => role switch
    {
        "Owner" => "owner",
        "Admin" => "admin",
        "Member" => "member",
        _ => role,
    };
}
