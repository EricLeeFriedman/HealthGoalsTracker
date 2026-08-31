using HealthGoalsTracker.Functions.Contracts;
using HealthGoalsTracker.Functions.Functions;
using HealthGoalsTracker.Functions.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace HealthGoalsTracker.Functions.Tests;

public class FunctionContractTests
{
    [Fact]
    public void Health_ReturnsStatusAndCorrelationHeaderWithoutAuthentication()
    {
        var context = BackendTestData.HttpContext(correlationId: "correlation-1");

        var result = new HealthFunction().Run(context.Request);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("healthy", Assert.IsType<HealthResponse>(ok.Value).Status);
        Assert.Equal("correlation-1", context.Response.Headers["X-Correlation-Id"]);
    }

    [Fact]
    public async Task Sync_ReturnsUnauthorizedWithoutValidatedIdentity()
    {
        var function = CreateSyncFunction();
        var context = BackendTestData.HttpContext(BackendTestData.SyncRequest());

        var result = await function.RunAsync(context.Request, CancellationToken.None);

        var unauthorized = Assert.IsType<ObjectResult>(result);
        Assert.Equal(401, unauthorized.StatusCode);
        Assert.Equal("unauthorized", Assert.IsType<ApiError>(unauthorized.Value).Code);
        Assert.True(context.Response.Headers.ContainsKey("X-Correlation-Id"));
    }

    [Fact]
    public async Task Sync_ReturnsStableErrorForMalformedJson()
    {
        var function = CreateSyncFunction();
        var context = BackendTestData.HttpContext("{", "user");

        var result = await function.RunAsync(context.Request, CancellationToken.None);

        var invalid = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("invalid_json", Assert.IsType<ApiError>(invalid.Value).Code);
    }

    [Theory]
    [InlineData("""{"deviceId":"00000000-0000-0000-0000-000000000001","goals":null,"dailyRecords":[],"measurements":[]}""")]
    [InlineData("""{"deviceId":"00000000-0000-0000-0000-000000000001","goals":[],"dailyRecords":null,"measurements":[]}""")]
    [InlineData("""{"deviceId":"00000000-0000-0000-0000-000000000001","goals":[],"dailyRecords":[],"measurements":null}""")]
    [InlineData("""{"deviceId":"00000000-0000-0000-0000-000000000001","goals":[],"dailyRecords":[{"id":"00000000-0000-0000-0000-000000000002","date":"2026-08-31","updatedAt":"2026-08-31T12:00:00Z","entries":null}],"measurements":[]}""")]
    [InlineData("""{"deviceId":"00000000-0000-0000-0000-000000000001","goals":[null],"dailyRecords":[],"measurements":[]}""")]
    [InlineData("""{"deviceId":"00000000-0000-0000-0000-000000000001","goals":[],"dailyRecords":[null],"measurements":[]}""")]
    [InlineData("""{"deviceId":"00000000-0000-0000-0000-000000000001","goals":[],"dailyRecords":[],"measurements":[null]}""")]
    [InlineData("""{"deviceId":"00000000-0000-0000-0000-000000000001","goals":[],"dailyRecords":[{"id":"00000000-0000-0000-0000-000000000002","date":"2026-08-31","updatedAt":"2026-08-31T12:00:00Z","entries":[null]}],"measurements":[]}""")]
    public async Task Sync_ReturnsValidationFailureForNullCollections(string json)
    {
        var function = CreateSyncFunction();
        var context = BackendTestData.HttpContext(json, "user");

        var result = await function.RunAsync(context.Request, CancellationToken.None);

        var invalid = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("validation_failed", Assert.IsType<ApiError>(invalid.Value).Code);
    }

    [Fact]
    public async Task Sync_ReturnsForbiddenWhenDelegatedScopeIsMissing()
    {
        var function = CreateSyncFunction();
        var context = BackendTestData.HttpContext(BackendTestData.SyncRequest());
        context.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
                [new System.Security.Claims.Claim("sub", "user")],
                "test"));

        var result = await function.RunAsync(context.Request, CancellationToken.None);

        var forbidden = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, forbidden.StatusCode);
        Assert.Equal("forbidden", Assert.IsType<ApiError>(forbidden.Value).Code);
    }

    [Fact]
    public async Task DevelopmentHeaderIsRejectedOutsideDevelopmentEnvironment()
    {
        var function = CreateSyncFunction(
            allowDevelopmentIdentity: true,
            environment: "Production");
        var context = BackendTestData.HttpContext(BackendTestData.SyncRequest());
        context.Request.Headers["X-HealthGoals-Test-Subject"] = "local-test-user";

        var result = await function.RunAsync(context.Request, CancellationToken.None);

        Assert.Equal(401, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public async Task EasyAuthPrincipalHeaderSuppliesValidatedSubjectAndScope()
    {
        var function = CreateSyncFunction(websiteAuthEnabled: true);
        var context = BackendTestData.HttpContext(BackendTestData.SyncRequest());
        var principal = System.Text.Json.JsonSerializer.Serialize(new
        {
            claims = new[]
            {
                new { typ = "sub", val = "easy-auth-user" },
                new
                {
                    typ = "http://schemas.microsoft.com/identity/claims/scope",
                    val = BackendTestData.ApiScope
                }
            }
        });
        context.Request.Headers["X-MS-CLIENT-PRINCIPAL"] =
            Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(principal));

        var result = await function.RunAsync(context.Request, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task EasyAuthPrincipalHeaderIsNotTrustedWhenPlatformAuthIsDisabled()
    {
        var function = CreateSyncFunction();
        var context = BackendTestData.HttpContext(BackendTestData.SyncRequest());
        context.Request.Headers["X-MS-CLIENT-PRINCIPAL"] =
            Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(
                """{"claims":[{"typ":"sub","val":"spoofed"},{"typ":"scp","val":"health-goals.sync"}]}"""));

        var result = await function.RunAsync(context.Request, CancellationToken.None);

        Assert.Equal(401, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public async Task Sync_ValidationFailureDoesNotPartiallyApplyValidEntities()
    {
        var repository = BackendTestData.Repository();
        var function = CreateSyncFunction(repository);
        var request = BackendTestData.SyncRequest();
        request.Goals.Add(BackendTestData.Goal());
        request.Measurements.Add(BackendTestData.Measurement(id: "invalid"));
        var context = BackendTestData.HttpContext(request, "user");

        var result = await function.RunAsync(context.Request, CancellationToken.None);

        var invalid = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("validation_failed", Assert.IsType<ApiError>(invalid.Value).Code);
        Assert.Empty(await repository.GetGoalsAsync("user", CancellationToken.None));
    }

    [Fact]
    public async Task DevelopmentHeaderRequiresExplicitConfiguration()
    {
        var request = BackendTestData.SyncRequest();
        var disabledFunction = CreateSyncFunction(allowDevelopmentIdentity: false);
        var disabledContext = BackendTestData.HttpContext(request);
        disabledContext.Request.Headers["X-HealthGoals-Test-Subject"] = "local-test-user";

        var disabled = await disabledFunction.RunAsync(
            disabledContext.Request,
            CancellationToken.None);

        Assert.Equal(401, Assert.IsType<ObjectResult>(disabled).StatusCode);

        var enabledFunction = CreateSyncFunction(allowDevelopmentIdentity: true);
        var enabledContext = BackendTestData.HttpContext(request);
        enabledContext.Request.Headers["X-HealthGoals-Test-Subject"] = "local-test-user";

        var enabled = await enabledFunction.RunAsync(
            enabledContext.Request,
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(enabled);
    }

    [Fact]
    public async Task ReadFunctions_RequireDatesAndPreserveCorrelationId()
    {
        var repository = BackendTestData.Repository();
        var functions = CreateReadFunctions(repository);
        var context = BackendTestData.HttpContext(subject: "user", correlationId: "read-1");

        var result = await functions.GetRecordsAsync(
            context.Request,
            CancellationToken.None);

        var invalid = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ApiError>(invalid.Value);
        Assert.Equal("validation_failed", error.Code);
        Assert.Contains("from", error.Details!);
        Assert.Equal("read-1", context.Response.Headers["X-Correlation-Id"]);
    }

    public static SyncFunction CreateSyncFunction(
        InMemoryCloudRepository? repository = null,
        bool allowDevelopmentIdentity = false,
        string environment = "Development",
        bool websiteAuthEnabled = false)
    {
        repository ??= BackendTestData.Repository();
        var cursorCodec = BackendTestData.CursorCodec();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AllowDevelopmentIdentity"] = allowDevelopmentIdentity.ToString(),
                ["AZURE_FUNCTIONS_ENVIRONMENT"] = environment,
                ["ApiScope"] = BackendTestData.ApiScope,
                ["WEBSITE_AUTH_ENABLED"] = websiteAuthEnabled.ToString()
            })
            .Build();
        return new SyncFunction(
            new CloudApiService(repository, cursorCodec),
            new RequestIdentityResolver(configuration),
            NullLogger<SyncFunction>.Instance);
    }

    public static ReadFunctions CreateReadFunctions(InMemoryCloudRepository repository)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ApiScope"] = BackendTestData.ApiScope
            })
            .Build();
        return new ReadFunctions(
            new CloudApiService(repository, BackendTestData.CursorCodec()),
            new RequestIdentityResolver(configuration));
    }
}
