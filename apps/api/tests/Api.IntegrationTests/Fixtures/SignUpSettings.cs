namespace Api.IntegrationTests.Fixtures;

// The product's sign-up switch (step 40d), off by default.
internal static class SignUpSettings
{
    public static readonly IReadOnlyDictionary<string, string?> On = new Dictionary<string, string?> { ["ControlPlane:SignUp:Enabled"] = "true" };

    // Sign-up on, with a limit of one tenant for each person, so a test reaches the limit with one creation.
    public static readonly IReadOnlyDictionary<string, string?> OnWithOneTenant = new Dictionary<string, string?>
    {
        ["ControlPlane:SignUp:Enabled"] = "true",
        ["ControlPlane:SignUp:TenantLimit"] = "1",
    };
}
