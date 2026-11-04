namespace ControlPlane.Api.Me.Users;

/// <summary>The six digits that went to the person's address.</summary>
public sealed record ConfirmAccountDeletionRequest(string Code);
