using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace ControlPlane.Domain.FeatureFlags;

/// <summary>
/// The bucket of an organisation for a flag, from 0 to 99. The percentage rule turns the flag on for every organisation whose bucket is
/// under the percentage. The bucket hashes the flag's key and the organisation's id, so it is the same in every process and at every read,
/// and an organisation that is in stays in as the percentage grows (Fowler and Hodgson, "Feature Toggles", "Percentage rollouts"). The
/// percentage counts organisations, not people, so everyone in one organisation sees the same screens. The key is part of the hash, so
/// the first 10 percent of one flag are not the first 10 percent of every flag.
/// </summary>
internal static class FeatureFlagBucket
{
    public const int Count = 100;

    public static int Of(string key, Guid tenantId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{key}:{tenantId:D}"));
        return (int)(BinaryPrimitives.ReadUInt32BigEndian(hash) % Count);
    }
}
