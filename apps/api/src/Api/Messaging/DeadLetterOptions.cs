namespace Api.Messaging;

// How long Wolverine keeps a dead letter (Host:Messaging:DeadLetters, W13).
internal sealed class DeadLetterOptions
{
    public const string Section = "Host:Messaging:DeadLetters";

    public TimeSpan Retention { get; set; } = TimeSpan.FromDays(14);
}
