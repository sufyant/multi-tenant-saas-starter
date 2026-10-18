namespace ControlPlane.Contracts.Members;

/// <summary>
/// A role: the built-in role, or null for a custom role, which comes later (A3), and the role's stored name. A built-in role answers
/// <c>{ "builtIn": "owner", "name": "owner" }</c>. A client compares <c>builtIn</c> with <see cref="MemberRole"/>, never the name.
/// </summary>
public sealed record RoleResponse(MemberRole? BuiltIn, string Name);
