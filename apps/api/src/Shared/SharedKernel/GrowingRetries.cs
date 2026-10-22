namespace SharedKernel;

/// <summary>
/// The pauses of a retry that grows, for a call to a service that may be down for a while, such as the identity provider or the email
/// service (W11). A pause shorter than <see cref="LongestInlinePause"/> is waited out where the handler runs; a longer one is a scheduled
/// retry, so the message holds no worker meanwhile. Each module wires the two lists into Wolverine for its own messages, and keeps its
/// own defaults.
/// </summary>
public sealed record GrowingRetries(IReadOnlyList<TimeSpan> Inline, IReadOnlyList<TimeSpan> Scheduled)
{
    public static TimeSpan LongestInlinePause { get; } = TimeSpan.FromMinutes(1);

    /// <summary>At least one pause, the first one positive, and each one longer than the one before.</summary>
    public static bool AreValid(IReadOnlyList<TimeSpan> delays) =>
        delays.Count > 0 && delays[0] > TimeSpan.Zero && delays.Zip(delays.Skip(1)).All(pair => pair.Second > pair.First);

    /// <summary>The pauses, split into the inline pauses at the start and the scheduled pauses after them.</summary>
    public static GrowingRetries Of(IReadOnlyList<TimeSpan> delays)
    {
        TimeSpan[] inline = [.. delays.TakeWhile(delay => delay < LongestInlinePause)];
        return new(inline, [.. delays.Skip(inline.Length)]);
    }
}
