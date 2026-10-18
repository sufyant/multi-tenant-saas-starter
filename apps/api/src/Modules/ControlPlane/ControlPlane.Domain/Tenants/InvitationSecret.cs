using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace ControlPlane.Domain.Tenants;

/// <summary>
/// The secret an invitation link carries. Only its hash is stored; 256 random bits make a fast hash enough, since there is
/// nothing to guess.
/// </summary>
internal static class InvitationSecret
{
    public static string Generate() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    public static string Hash(string secret) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));
}
