namespace Audit.Domain;

/// <summary>
/// The operations of the product's audit entries, beside the template's in <see cref="AuditOperations"/> (X6), with the same rules: each
/// declared once, area.action, and a stored name never changes. The template ships this class empty and never changes it: a product
/// declares its operations here. A product's operation keeps no details in step 41.
/// </summary>
public static class ProductAuditOperations
{
    /// <summary>A member created a project.</summary>
    public const string ProjectCreated = "project.created";
}
