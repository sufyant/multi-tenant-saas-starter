using ControlPlane.Domain.EmailAddresses;
using ControlPlane.Domain.Roles;
using ControlPlane.Domain.Tenants;
using SharedKernel;

namespace ControlPlane.UnitTests.Tenants;

// R12: the summary says what the tenant's rows say. The latest Owner invitation stays only while the tenant has no Owner: its address
// is a person's (the maintainer's decision in step 20k).
public sealed class TenantSummaryTests
{
    private static readonly Guid TenantId = new("0199a8f0-0000-7000-8000-000000000201");

    private static readonly DateTimeOffset Now = new(2026, 10, 16, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SummarizeTenant_WithoutAnOwner_KeepsTheCountsAndTheLatestOwnerInvitation()
    {
        var summary = new TenantSummary(TenantId);

        summary.Summarize(0, 0, OwnerInvitation());

        (summary.MemberCount, summary.OwnerCount).ShouldBe((0, 0));
        (summary.OwnerInvitationEmail, summary.OwnerInvitationExpiresAt, summary.OwnerInvitationStatus)
            .ShouldBe(("ada@example.com", Now.AddDays(7), InvitationStatus.Pending));
    }

    [Fact]
    public void SummarizeTenant_AnOwnerJoined_KeepsTheCountsAndClearsTheOwnerInvitation()
    {
        var summary = new TenantSummary(TenantId);
        summary.Summarize(0, 0, OwnerInvitation());

        summary.Summarize(3, 2, OwnerInvitation());

        (summary.MemberCount, summary.OwnerCount).ShouldBe((3, 2));
        (summary.OwnerInvitationEmail, summary.OwnerInvitationExpiresAt, summary.OwnerInvitationStatus).ShouldBe((null, null, null));
        summary.OwnerInvitationStateAt(Now).ShouldBeNull();
    }

    // "Expired" is read from the expiry at the moment it is asked, never stored.
    [Theory]
    [InlineData(0, "Pending")]
    [InlineData(7, "Expired")]
    [InlineData(8, "Expired")]
    public void ReadOwnerInvitationState_APendingInvitation_IsExpiredFromItsExpiryOn(int days, string state)
    {
        var summary = new TenantSummary(TenantId);
        summary.Summarize(0, 0, OwnerInvitation());

        summary.OwnerInvitationStateAt(Now.AddDays(days)).ShouldBe(Enum.Parse<OwnerInvitationState>(state));
    }

    [Fact]
    public void ReadOwnerInvitationState_ACancelledInvitation_IsCancelled()
    {
        var summary = new TenantSummary(TenantId);
        var invitation = OwnerInvitation();
        invitation.Cancel();
        summary.Summarize(0, 0, invitation);

        summary.OwnerInvitationStateAt(Now).ShouldBe(OwnerInvitationState.Cancelled);
    }

    private static Invitation OwnerInvitation() =>
        Invitation.Create(Guid.CreateVersion7(), TenantId, EmailAddress.Create("ada@example.com"), BuiltInRoles.Owner, Guid.CreateVersion7(), Now, TimeSpan.FromDays(7), "secret", Language.En);
}
