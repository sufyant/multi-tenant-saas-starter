using System.Buffers.Binary;
using System.Buffers.Text;
using System.Security.Cryptography;

namespace SharedKernel;

/// <summary>
/// A place in a list ordered newest first, by a moment and then an id, both descending: the pair of the last item a page held. The next
/// page starts after it, so an item added between two requests never moves another item to another page (K5, the audit logs).
/// </summary>
public readonly record struct CursorPosition(DateTimeOffset At, Guid Id);

/// <summary>What a client asks of a cursor list: how many items, and the place after which the page starts; null is the first page.</summary>
public sealed record CursorRequest(int Limit, CursorPosition? After)
{
    public const int MinLimit = 1;

    public const int DefaultLimit = 50;

    public const int MaxLimit = 100;

    /// <summary>
    /// The page the client asked for, from what it sent. A limit left out takes the default. A cursor left out or empty is the first
    /// page. A cursor that does not decode, or was changed, is refused.
    /// </summary>
    public static Result<CursorRequest> Create(string? cursor, int? limit)
    {
        if (limit is < MinLimit or > MaxLimit)
        {
            return Error.Validation(PagingErrorCodes.LimitInvalid, $"The limit is {MinLimit} to {MaxLimit}.");
        }

        if (string.IsNullOrEmpty(cursor))
        {
            return new CursorRequest(limit ?? DefaultLimit, null);
        }

        return Cursor.Decode(cursor) is { } after
            ? new CursorRequest(limit ?? DefaultLimit, after)
            : Error.Validation(PagingErrorCodes.CursorInvalid, "The cursor is not one this list gave.");
    }
}

/// <summary>One page of a cursor list, and the cursor of the next page; null on the last page. There is no total.</summary>
public sealed record CursorPage<T>(IReadOnlyList<T> Items, string? NextCursor)
{
    public CursorPage<TOut> Map<TOut>(Func<T, TOut> map) => new([.. Items.Select(map)], NextCursor);
}

public static class CursorPage
{
    /// <summary>
    /// The page from the items a reader found in order, one more than the limit when it could: that one tells that a next page exists,
    /// and is not on this page.
    /// </summary>
    public static CursorPage<T> Of<T>(IReadOnlyList<T> found, CursorRequest request, Func<T, CursorPosition> positionOf)
    {
        if (found.Count <= request.Limit)
        {
            return new CursorPage<T>(found, null);
        }

        var items = found.Take(request.Limit).ToList();
        return new CursorPage<T>(items, Cursor.Encode(positionOf(items[^1])));
    }
}

/// <summary>
/// The text of a cursor: a version, the moment in UTC ticks, the id and a checksum of those, in base64url. A client treats it as opaque.
/// The checksum finds a cursor that was changed. It is not a secret: a forged cursor only moves the page within what the caller may read.
/// </summary>
public static class Cursor
{
    private const byte Version = 1;

    private const int ChecksumLength = 4;

    private const int PayloadLength = 1 + sizeof(long) + 16;

    private const int Length = PayloadLength + ChecksumLength;

    public static string Encode(CursorPosition position)
    {
        Span<byte> bytes = stackalloc byte[Length];
        bytes[0] = Version;
        BinaryPrimitives.WriteInt64BigEndian(bytes[1..], position.At.UtcTicks);
        position.Id.TryWriteBytes(bytes[(1 + sizeof(long))..PayloadLength], bigEndian: true, out _);
        Checksum(bytes[..PayloadLength]).CopyTo(bytes[PayloadLength..]);
        return Base64Url.EncodeToString(bytes);
    }

    /// <summary>The place the text names; null when it does not decode or was changed.</summary>
    public static CursorPosition? Decode(string text)
    {
        // The decoder throws on a character outside base64url, so the text is checked first.
        if (!Base64Url.IsValid(text, out var length) || length != Length)
        {
            return null;
        }

        Span<byte> bytes = stackalloc byte[Length];
        if (!Base64Url.TryDecodeFromChars(text, bytes, out var written)
            || written != Length
            || bytes[0] != Version
            || !Checksum(bytes[..PayloadLength]).SequenceEqual(bytes[PayloadLength..Length]))
        {
            return null;
        }

        var ticks = BinaryPrimitives.ReadInt64BigEndian(bytes[1..]);
        if (ticks is < 0 || ticks > DateTimeOffset.MaxValue.UtcTicks)
        {
            return null;
        }

        return new CursorPosition(new DateTimeOffset(ticks, TimeSpan.Zero), new Guid(bytes[(1 + sizeof(long))..PayloadLength], bigEndian: true));
    }

    private static byte[] Checksum(ReadOnlySpan<byte> payload) => SHA256.HashData(payload)[..ChecksumLength];
}
