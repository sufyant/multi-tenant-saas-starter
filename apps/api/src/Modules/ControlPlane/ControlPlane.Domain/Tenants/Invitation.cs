using ControlPlane.Domain.EmailAddresses;
using ControlPlane.Domain.Roles;
using SharedKernel;

namespace ControlPlane.Domain.Tenants;

/// <summary>
/// An invitation to join a tenant with a role. It is single-use and expires. Its secret is born with it and only the secret's hash is
/// kept. Accepting it needs both the secret and a verified email address of the accepting user that matches the invited one, so a
/// forwarded link does not let someone else in.
/// </summary>
internal sealed class Invitation : ITenantEntity
{
    /// <summary>No invitation answers the code or the id: none was found, or it is another tenant's, or it was cancelled (K2).</summary>
    public static readonly Error NotFound = Error.NotFound(ControlPlaneErrorCodes.InvitationNotFound, "The invitation was not found.");

    // The body holds no address, not even part of one: the person learns only that theirs is not the invited one.
    private static readonly Error EmailMismatch =
        Error.Forbidden(ControlPlaneErrorCodes.InvitationEmailMismatch, "The invitation was sent to an email address you have not verified.");

    private Invitation(
        Guid id,
        Guid tenantId,
        string email,
        Guid? roleId,
        Guid invitedBy,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        string secretHash,
        Language language)
    {
        Id = id;
        TenantId = tenantId;
        Email = email;
        RoleId = roleId;
        InvitedBy = invitedBy;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
        SecretHash = secretHash;
        Language = language;
        Status = InvitationStatus.Pending;
    }

    public Guid Id { get; private init; }

    public Guid TenantId { get; private init; }

    /// <summary>The invited address, in its one form (<see cref="EmailAddress"/>).</summary>
    public string Email { get; private init; }

    /// <summary>
    /// The role the invitation gives. A pending invitation always has one (a CHECK). A finished invitation keeps none once its custom role
    /// is deleted (ON DELETE SET NULL); its audit entries keep the role's name (K3).
    /// </summary>
    public Guid? RoleId { get; private init; }

    /// <summary>
    /// The SHA-256 hash of the secret the invitation's link carries. Only an invitation from before secrets were born with their
    /// invitation has none; it can never be accepted.
    /// </summary>
    public string? SecretHash { get; private init; }

    public Guid InvitedBy { get; private init; }

    public DateTimeOffset CreatedAt { get; private init; }

    public DateTimeOffset ExpiresAt { get; private init; }

    public InvitationStatus Status { get; private set; }

    /// <summary>The language of the invitation's email. A person who is not a user yet starts with it when they accept.</summary>
    public Language Language { get; private init; }

    public DateTimeOffset? AcceptedAt { get; private set; }

    public Guid? AcceptedBy { get; private set; }

    /// <summary>
    /// The identity provider's invitation that lets the invited person sign up. None while the provider has not answered, and none when
    /// the address already had an account.
    /// </summary>
    public string? SignUpInvitationId { get; private set; }

    public static Invitation Create(
        Guid id,
        Guid tenantId,
        EmailAddress email,
        Role role,
        Guid invitedBy,
        DateTimeOffset now,
        TimeSpan lifetime,
        string secret,
        Language language) =>
        new(id, tenantId, email.Value, role.Id, invitedBy, now, now + lifetime, InvitationSecret.Hash(secret), language);

    /// <summary>
    /// Whether a member of the inviter's role may invite someone with the role (K1): only a member whose role holds
    /// <c>tenant.owners.manage</c> gives a role that holds it (A1). It changes nothing, so a caller checks it before any change.
    /// </summary>
    public static Result CheckInviter(Role inviter, Role role) =>
        role.Holds(PermissionPools.TenantOwnersManage) && !inviter.Holds(PermissionPools.TenantOwnersManage)
            ? Error.Forbidden(ControlPlaneErrorCodes.InvitationOwnerRequiresOwner, "Only an Owner can invite an Owner.")
            : Result.Success();

    /// <summary>
    /// Whether one of these verified email addresses is the invited address, both in their one form, whatever the invitation's status. The
    /// preview asks it of the invitation it read, before it reads anything else, so a holder of the link without the invited address learns
    /// nothing more than the email told them; the acceptance asks it last, with the same error.
    /// </summary>
    public static Result CheckEmail(string invitedEmail, IEnumerable<EmailAddress> verifiedEmails) =>
        verifiedEmails.Any(email => email.Value == invitedEmail) ? Result.Success() : EmailMismatch;

    /// <summary>Cancels a pending invitation, as a replacement or a closure does. A used or cancelled one stays as it is.</summary>
    public StateChange Cancel()
    {
        if (Status != InvitationStatus.Pending)
        {
            return StateChange.Unchanged;
        }

        Status = InvitationStatus.Cancelled;
        return StateChange.Changed;
    }

    /// <summary>
    /// A member revokes the invitation, so its link stops working at once (K2). A pending one, expired or not, is cancelled: Changed. A
    /// cancelled one stays as it is: Unchanged, as setting a state twice changes nothing (I1). A used one cannot be revoked.
    /// </summary>
    public Result<StateChange> Revoke() => Status switch
    {
        InvitationStatus.Accepted => Error.Conflict(ControlPlaneErrorCodes.InvitationNotPending, "The invitation has already been used."),
        _ => Cancel(),
    };

    /// <summary>
    /// Takes the identity provider's answer for the invitation's sign-up: its invitation, or none for an address that has an account.
    /// The link goes out only for a pending invitation that has no other provider invitation. An answer that arrives twice changes
    /// nothing. A provider invitation that comes too late, for an invitation that is no longer pending or already has one, must be
    /// withdrawn, so nobody signs up with a link nobody receives.
    /// </summary>
    public SignUpDecision RecordSignUp(string? signUpInvitationId)
    {
        if (signUpInvitationId is not null && signUpInvitationId == SignUpInvitationId)
        {
            return SignUpDecision.Ignore;
        }

        if (Status != InvitationStatus.Pending || SignUpInvitationId is not null)
        {
            return signUpInvitationId is null ? SignUpDecision.Ignore : SignUpDecision.Withdraw;
        }

        SignUpInvitationId = signUpInvitationId;
        return SignUpDecision.Send;
    }

    /// <summary>
    /// The identity provider's invitation to withdraw at the end of the invitation's lifetime: an invitation still pending keeps it until
    /// then. A revoked, replaced or cancelled one withdrew it when it ended, an accepted one no longer needs it, and before the expiry by
    /// this clock nothing is withdrawn. None when nothing is withdrawn.
    /// </summary>
    public string? SignUpToWithdrawAt(DateTimeOffset now) =>
        Status == InvitationStatus.Pending && now >= ExpiresAt ? SignUpInvitationId : null;

    /// <summary>
    /// Whether the invitation can be accepted now by someone with these verified email addresses: it is pending, it has not expired,
    /// and one of the addresses is the invited one. It changes nothing, so a caller can check more after it and still refuse
    /// without a change.
    /// </summary>
    public Result CheckAcceptance(IEnumerable<EmailAddress> verifiedEmails, DateTimeOffset now)
    {
        if (Status != InvitationStatus.Pending)
        {
            return Error.Conflict(ControlPlaneErrorCodes.InvitationNotPending, "The invitation has already been used.");
        }

        if (now >= ExpiresAt)
        {
            return Error.Conflict(ControlPlaneErrorCodes.InvitationExpired, "The invitation has expired; ask for a new one.");
        }

        return CheckEmail(verifiedEmails);
    }

    /// <summary>Whether one of these verified email addresses is this invitation's address (<see cref="CheckEmail(string, IEnumerable{EmailAddress})"/>).</summary>
    public Result CheckEmail(IEnumerable<EmailAddress> verifiedEmails) => CheckEmail(Email, verifiedEmails);

    /// <summary>The state of the invitation at this moment: a pending invitation is expired from its expiry on.</summary>
    public InvitationState StateAt(DateTimeOffset now) => Status switch
    {
        InvitationStatus.Pending => now >= ExpiresAt ? InvitationState.Expired : InvitationState.Pending,
        InvitationStatus.Accepted => InvitationState.Accepted,
        _ => InvitationState.Cancelled,
    };

    /// <summary>Uses up the invitation for the user, after the same checks as <see cref="CheckAcceptance"/>.</summary>
    public Result Accept(IEnumerable<EmailAddress> verifiedEmails, Guid userId, DateTimeOffset now)
    {
        var allowed = CheckAcceptance(verifiedEmails, now);
        if (!allowed.IsSuccess)
        {
            return allowed;
        }

        Status = InvitationStatus.Accepted;
        AcceptedAt = now;
        AcceptedBy = userId;
        return Result.Success();
    }
}
