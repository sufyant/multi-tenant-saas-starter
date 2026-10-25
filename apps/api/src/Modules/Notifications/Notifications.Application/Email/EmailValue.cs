namespace Notifications.Application.Email;

/// <summary>A value that fills the placeholder <c>{{Name}}</c> of a generated email.</summary>
internal sealed record EmailValue(string Name, string Value, EmailValueKind Kind)
{
    public static EmailValue Text(string name, string value) => new(name, value, EmailValueKind.Text);

    public static EmailValue Link(string name, Uri value) => new(name, value.OriginalString, EmailValueKind.Link);

    public static EmailValue Contact(string name, Uri value) => new(name, value.OriginalString, EmailValueKind.Contact);
}
