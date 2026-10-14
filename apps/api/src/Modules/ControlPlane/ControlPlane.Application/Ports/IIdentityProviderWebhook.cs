using ControlPlane.Application.Users;
using SharedKernel;

namespace ControlPlane.Application.Ports;

/// <summary>The identity provider's webhook: the signed events it sends when one of its users changes.</summary>
public interface IIdentityProviderWebhook
{
    /// <summary>
    /// Checks the request's signature, then reads its event. A missing or wrong signature, or a timestamp outside the tolerance, is a
    /// failure. An event the user profile copy does not follow reads as no command.
    /// </summary>
    Result<UpdateUserProfile?> Read(string payload, Func<string, string?> header);
}
