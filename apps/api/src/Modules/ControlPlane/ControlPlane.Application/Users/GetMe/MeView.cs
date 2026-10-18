using SharedKernel;

namespace ControlPlane.Application.Users.GetMe;

/// <summary>The signed-in user: the catalog's id, the copy of their profile, whether they are a system admin, and their preferences.</summary>
public sealed record MeView(Guid Id, string? Email, string? FirstName, string? LastName, bool IsSystemAdmin, Language Language, string? TimeZone);
