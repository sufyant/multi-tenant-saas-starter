using SharedKernel;

namespace ControlPlane.Application.Members.ExportMembers;

/// <summary>A member who reads the members exports them to a file; the command records it, and the web's server writes the file.</summary>
[MessageName("export-members")]
public sealed record ExportMembersCommand(string ActorId);
