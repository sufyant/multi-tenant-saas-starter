using SharedKernel;

namespace ControlPlane.Application.Users;

/// <summary>
/// The user who sends a command, as the command reads them: their id, their language, which an invitation they send takes when the
/// command names none, and their names, which the view of that invitation shows. It is a value, not the user aggregate (K6a).
/// </summary>
internal sealed record Actor(Guid Id, Language Language, UserNameView Name);
