namespace ControlPlane.Application.Tenants.TenantCreation;

public enum TenantCreationSagaState
{
    Activating,
    SendingInvitation,
    Completed,
    Cancelling,
    Cancelled,
    NeedsAttention,
}
