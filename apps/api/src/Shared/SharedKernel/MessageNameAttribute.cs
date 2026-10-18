namespace SharedKernel;

/// <summary>
/// The fixed name of a message on the wire, in kebab case (<c>member-invited</c>). The messaging tool sends and stores a message by this
/// name, not by its type's name, so a moved or renamed type keeps its name (W12). It is ours, not the tool's, because a module's
/// Contracts reference only SharedKernel (section 2).
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class MessageNameAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}
