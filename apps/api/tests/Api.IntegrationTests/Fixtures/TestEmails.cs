using System.Security.Cryptography;
using System.Text;

namespace Api.IntegrationTests.Fixtures;

// An address of the running test. The shared database keeps every test's users, and two live users never keep one address (A7, the unique
// index ux_users_email), so a test that writes a user's address writes one of its own: the address as the test reads it, with a tag of the
// test case in its local part. The same address in one test case gives the same result, so the test compares what it wrote.
internal static class TestEmails
{
    public static string Of(string email)
    {
        var at = email.LastIndexOf('@');
        return $"{email[..at]}+{Tag()}{email[at..]}";
    }

    private static string Tag() =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(TestContext.Current.Test?.UniqueID ?? "outside a test")))[..12];
}
