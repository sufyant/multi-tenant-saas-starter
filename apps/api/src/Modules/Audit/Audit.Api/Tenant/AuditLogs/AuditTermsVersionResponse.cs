namespace Audit.Api.Tenant.AuditLogs;

/// <summary>A terms version's values as an entry keeps them: its day, its link, its summary or null, whether it requires acceptance, and whether it emails the users.</summary>
public sealed record AuditTermsVersionResponse(DateOnly EffectiveDate, string Url, string? Summary, bool RequiresAcceptance, bool SendsEmail);
