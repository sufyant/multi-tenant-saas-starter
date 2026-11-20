using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tenancy;

namespace Api.IntegrationTests.Public;

// GET /v1/public/sign-up: the entry pages ask before sign-in whether the product has sign-up on (1.24), and how many organisations a person
// may create (1.28), and the trial a new organisation starts while billing is on (1.28, 4.29).
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

    // 1.28: "Your 14-day Business trial starts when you create it." The plan and the length come from the configuration.
    [Fact]
    public async Task GetSignUp_BillingOn_AnswersTheTrialPlanAndItsDays()
    {
        await using var api = new ApiFactory(
            database.ConnectionStringFor(DatabaseRoles.Application), settings: new Dictionary<string, string?>(SignUpSettings.On.Concat(BillingRequests.On)));

        var signUp = await api.CreateClient().GetFromJsonAsync<JsonElement>("/v1/public/sign-up", Cancellation);

        var trial = signUp.GetProperty("trial");
        (trial.GetProperty("plan").GetProperty("id").GetGuid(), trial.GetProperty("plan").GetProperty("name").GetString(), trial.GetProperty("days").GetInt32())
            .ShouldBe((SeedPlans.Business, "Business", 14));
    }

    // A trial exists only while billing is on (the maintainer's decision of 2026-11-20).
    [Fact]
    public async Task GetSignUp_BillingOff_AnswersNoTrial()
    {
        await using var api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application), settings: SignUpSettings.On);

        var signUp = await api.CreateClient().GetFromJsonAsync<JsonElement>("/v1/public/sign-up", Cancellation);

        signUp.GetProperty("trial").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    // A trial plan of the configuration that no plan row has is no trial: the page offers none.
    [Fact]
    public async Task GetSignUp_TrialPlanUnknown_AnswersNoTrial()
    {
        await using var api = new ApiFactory(
            database.ConnectionStringFor(DatabaseRoles.Application),
            settings: new Dictionary<string, string?>(SignUpSettings.On.Concat(BillingRequests.On))
            {
                ["ControlPlane:Trial:PlanId"] = "0198f0a2-0000-7000-8000-00000000dead",
            });

        var signUp = await api.CreateClient().GetFromJsonAsync<JsonElement>("/v1/public/sign-up", Cancellation);

        signUp.GetProperty("trial").ValueKind.ShouldBe(JsonValueKind.Null);
    }
}
