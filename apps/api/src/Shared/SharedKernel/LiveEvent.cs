namespace SharedKernel;

/// <summary>One live event: its name and its data, which a stream writes as an event of server-sent events.</summary>
public sealed record LiveEvent(string Name, string Data);
