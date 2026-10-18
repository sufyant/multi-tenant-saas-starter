using System.Text.Json.Serialization;

namespace ControlPlane.Infrastructure.Clerk;

internal sealed record ClerkVerification([property: JsonPropertyName("status")] string Status);
