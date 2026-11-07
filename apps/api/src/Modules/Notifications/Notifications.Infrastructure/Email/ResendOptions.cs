using System.Net.Mail;

namespace Notifications.Infrastructure.Email;

// Resend's API (section Notifications:Resend), checked on start. The API key comes from the environment only, never from a committed
// file.
internal sealed class ResendOptions
{
    public const string Section = "Notifications:Resend";

    public string? ApiKey { get; set; }

    // The sender's address alone, for example no-reply@example.com, on a domain verified at Resend. The sender's name is the brand's.
    public string? FromAddress { get; set; }

    public Uri ApiUrl { get; set; } = new("https://api.resend.com/");

    // How long a send may take.
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(FromAddress);

    // An address without a name: the name comes from the brand, so a name here would be a second one.
    // An empty value is no address, as Development allows.
    public bool HasABareAddress => string.IsNullOrEmpty(FromAddress) || (MailAddress.TryCreate(FromAddress, out var address) && address.Address == FromAddress);
}
