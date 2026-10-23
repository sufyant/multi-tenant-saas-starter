namespace Api.IntegrationTests.Fixtures;

// Marks a test class whose fake clock starts at a fixed date. On the shared database a message the class schedules by that clock comes due
// in a later run, and a host of another test runs it. The reason says why the class may start there: a database of its own, or nothing
// it schedules (section 9, rule 10; FixedClockTests).
[AttributeUsage(AttributeTargets.Class)]
public sealed class FixedClockAttribute(string reason) : Attribute
{
    public string Reason { get; } = reason;
}
