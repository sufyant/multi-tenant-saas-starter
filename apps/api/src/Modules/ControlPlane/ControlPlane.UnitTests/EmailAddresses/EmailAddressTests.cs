using ControlPlane.Domain.EmailAddresses;

namespace ControlPlane.UnitTests.EmailAddresses;

public sealed class EmailAddressTests
{
    // An address takes one form where it enters: without the spaces around it, in lower case by the invariant culture. The invariant
    // culture keeps the Turkish dotted capital I (U+0130), which PostgreSQL's lower() would make an "i": so no query uses lower().
    [Theory]
    [InlineData("ali@example.com", "ali@example.com")]
    [InlineData("Ali@Example.COM", "ali@example.com")]
    [InlineData("  ali@example.com\t", "ali@example.com")]
    [InlineData("İpek@Example.com", "İpek@example.com")]
    [InlineData("IŞIK@example.com", "işik@example.com")]
    public void CreateEmailAddress_AnAddress_KeepsItTrimmedInLowerCase(string text, string kept)
    {
        var address = EmailAddress.TryCreate(text);

        address!.Value.ShouldBe(kept);
    }

    // The validators' check (FluentValidation's EmailAddress): one @, neither first nor last, and the length the column holds.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ali.example.com")]
    [InlineData("@example.com")]
    [InlineData("ali@")]
    [InlineData("ali@b@example.com")]
    public void CreateEmailAddress_NotAnAddress_IsNone(string text)
    {
        var address = EmailAddress.TryCreate(text);

        address.ShouldBeNull();
    }

    // The validators check the text as it came, spaces included, so every text they accept is an address here too: no handler fails
    // on an address the validator let through.
    [Theory]
    [InlineData(" @example.com", "@example.com")]
    [InlineData("ali@ ", "ali@")]
    public void CreateEmailAddress_ATextTheValidatorsAccept_IsAnAddress(string text, string kept)
    {
        var address = EmailAddress.TryCreate(text);

        address!.Value.ShouldBe(kept);
    }

    [Fact]
    public void CreateEmailAddress_LongerThanTheColumn_IsNone()
    {
        var address = EmailAddress.TryCreate($"{new string('a', EmailAddress.MaxLength - 11)}@example.com");

        address.ShouldBeNull();
    }

    // Two forms of one address are the same address, by plain equality.
    [Fact]
    public void CompareEmailAddresses_TwoCasesOfOneAddress_AreEqual()
    {
        var (upper, lower) = (EmailAddress.TryCreate("Ali@Example.com"), EmailAddress.TryCreate("ali@example.com"));

        upper.ShouldBe(lower);
    }

    // The identity provider's verified addresses enter in the same form; what is not an address is left out.
    [Fact]
    public void ReadVerifiedAddresses_InAnyCase_KeepsTheAddressesInTheirForm()
    {
        var verified = EmailAddress.AllOf(["Ali@Example.com", "not an address", " grace@example.com "]);

        verified.Select(address => address.Value).ShouldBe(["ali@example.com", "grace@example.com"]);
    }
}
