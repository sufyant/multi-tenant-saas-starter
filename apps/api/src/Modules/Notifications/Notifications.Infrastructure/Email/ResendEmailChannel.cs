using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Notifications.Application.Email;

namespace Notifications.Infrastructure.Email;

// Sends email through Resend's API, in HTML and in plain text, under the email's idempotency key: Resend sends one email per key. The
// sender is the brand's name (packages/brand/brand.json) and the configured address.
internal sealed class ResendEmailChannel(HttpClient http, IOptions<ResendOptions> options) : IEmailChannel
{
    public async Task SendAsync(EmailMessage email, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "emails")
        {
            Content = JsonContent.Create(new { from = $"{EmailTemplate.Brand.Name} <{options.Value.FromAddress}>", to = new[] { email.To }, subject = email.Subject, html = email.Html, text = email.Text }),
        };
        request.Headers.Add("Idempotency-Key", email.IdempotencyKey);

        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
