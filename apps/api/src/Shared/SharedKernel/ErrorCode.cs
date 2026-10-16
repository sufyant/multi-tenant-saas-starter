using System.Text.Json.Serialization;

namespace SharedKernel;

/// <summary>
/// Every code an error answer of the API can carry, in one list (K5). The OpenAPI document declares these values as one enum, so a
/// generated client knows each code, and a screen writes its own text for each one. Each value is <c>area.reason</c> in JSON.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ErrorCode>))]
public enum ErrorCode
{
    [JsonStringEnumMemberName("auth.unauthenticated")]
    AuthUnauthenticated,

    [JsonStringEnumMemberName("auth.forbidden")]
    AuthForbidden,

    [JsonStringEnumMemberName("idempotency_key_invalid")]
    IdempotencyKeyInvalid,

    [JsonStringEnumMemberName("idempotency_key_in_progress")]
    IdempotencyKeyInProgress,

    [JsonStringEnumMemberName("idempotency_key_reused")]
    IdempotencyKeyReused,

    [JsonStringEnumMemberName("invitation.code_required")]
    InvitationCodeRequired,

    [JsonStringEnumMemberName("invitation.daily_limit_reached")]
    InvitationDailyLimitReached,

    [JsonStringEnumMemberName("invitation.email_mismatch")]
    InvitationEmailMismatch,

    [JsonStringEnumMemberName("invitation.expired")]
    InvitationExpired,

    [JsonStringEnumMemberName("invitation.not_found")]
    InvitationNotFound,

    [JsonStringEnumMemberName("invitation.not_pending")]
    InvitationNotPending,

    [JsonStringEnumMemberName("invitation.owner_requires_owner")]
    InvitationOwnerRequiresOwner,

    [JsonStringEnumMemberName("invitation.staff_cannot_join")]
    InvitationStaffCannotJoin,

    [JsonStringEnumMemberName("membership.cannot_remove_self")]
    MembershipCannotRemoveSelf,

    [JsonStringEnumMemberName("membership.exists")]
    MembershipExists,

    [JsonStringEnumMemberName("membership.last_owner")]
    MembershipLastOwner,

    [JsonStringEnumMemberName("membership.not_found")]
    MembershipNotFound,

    [JsonStringEnumMemberName("membership.owner_requires_owner")]
    MembershipOwnerRequiresOwner,

    [JsonStringEnumMemberName("paging.cursor_invalid")]
    PagingCursorInvalid,

    [JsonStringEnumMemberName("paging.filter_invalid")]
    PagingFilterInvalid,

    [JsonStringEnumMemberName("paging.limit_invalid")]
    PagingLimitInvalid,

    [JsonStringEnumMemberName("paging.page_invalid")]
    PagingPageInvalid,

    [JsonStringEnumMemberName("paging.page_size_invalid")]
    PagingPageSizeInvalid,

    [JsonStringEnumMemberName("paging.q_invalid")]
    PagingQInvalid,

    [JsonStringEnumMemberName("paging.sort_invalid")]
    PagingSortInvalid,

    [JsonStringEnumMemberName("rate_limit.exceeded")]
    RateLimitExceeded,

    /// <summary>A request the endpoint cannot read, such as a body that is not JSON, or a value of the wrong type.</summary>
    [JsonStringEnumMemberName("request.invalid")]
    RequestInvalid,

    [JsonStringEnumMemberName("request.timeout")]
    RequestTimeout,

    [JsonStringEnumMemberName("request.too_large")]
    RequestTooLarge,

    [JsonStringEnumMemberName("request.unsupported_media_type")]
    RequestUnsupportedMediaType,

    /// <summary>Fields of the request failed validation; <c>errors</c> lists a <see cref="FieldErrorCode"/> for each.</summary>
    [JsonStringEnumMemberName("request.validation_failed")]
    RequestValidationFailed,

    [JsonStringEnumMemberName("route.method_not_allowed")]
    RouteMethodNotAllowed,

    [JsonStringEnumMemberName("route.not_found")]
    RouteNotFound,

    [JsonStringEnumMemberName("server.error")]
    ServerError,

    [JsonStringEnumMemberName("tenant.name_invalid")]
    TenantNameInvalid,

    [JsonStringEnumMemberName("tenant.not_active")]
    TenantNotActive,

    [JsonStringEnumMemberName("tenant.not_found")]
    TenantNotFound,

    [JsonStringEnumMemberName("tenant.slug_invalid")]
    TenantSlugInvalid,

    [JsonStringEnumMemberName("tenant.slug_taken")]
    TenantSlugTaken,

    [JsonStringEnumMemberName("tenant.suspended")]
    TenantSuspended,

    [JsonStringEnumMemberName("user.is_system_admin")]
    UserIsSystemAdmin,

    [JsonStringEnumMemberName("user.not_found")]
    UserNotFound,

    [JsonStringEnumMemberName("user.suspended")]
    UserSuspended,

    /// <summary>A webhook's signature is missing, wrong or too old. Only the webhooks answer it, and they are not in the document (N5).</summary>
    [JsonStringEnumMemberName("webhook.signature_invalid")]
    WebhookSignatureInvalid,
}
