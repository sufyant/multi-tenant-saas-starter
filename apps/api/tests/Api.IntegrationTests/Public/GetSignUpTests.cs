using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tenancy;

namespace Api.IntegrationTests.Public;

// GET /v1/public/sign-up: the entry pages ask before sign-in whether the product has sign-up on (1.24), and how many organisations a person
// may create (1.28).
public sealed class GetSignUpTests(Database database)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetSignUp_ByDefault_AnswersOffWithTheDefaultLimit()
    {
        await using var api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application));

        var answer = await api.CreateClient().GetAsync("/v1/public/sign-up", Cancellation);

        answer.StatusCode.ShouldBe(HttpStatusCode.OK);
        var signUp = await answer.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        (signUp.GetProperty("enabled").GetBoolean(), signUp.GetProperty("tenantLimit").GetInt32()).ShouldBe((false, 3));
    }

    [Fact]
    public async Task GetSignUp_SignUpOn_AnswersOnWithTheLimitOfTheConfiguration()
    {
        await using var api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application), settings: SignUpSettings.OnWithOneTenant);

        var signUp = await api.CreateClient().GetFromJsonAsync<JsonElement>("/v1/public/sign-up", Cancellation);

        (signUp.GetProperty("enabled").GetBoolean(), signUp.GetProperty("tenantLimit").GetInt32()).ShouldBe((true, 1));
    }
}
