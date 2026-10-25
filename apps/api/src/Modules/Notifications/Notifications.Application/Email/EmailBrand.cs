namespace Notifications.Application.Email;

/// <summary>The product's name and support contact, from <c>packages/brand</c>.</summary>
internal sealed record EmailBrand(string Name, Uri SupportUrl);
