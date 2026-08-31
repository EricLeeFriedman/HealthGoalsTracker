using HealthGoalsTracker.Functions.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace HealthGoalsTracker.Functions.Functions;

public class ReadFunctions : ApiFunctionBase
{
    public CloudApiService ApiService { get; }

    public ReadFunctions(
        CloudApiService apiService,
        RequestIdentityResolver identityResolver)
        : base(identityResolver)
    {
        ApiService = apiService;
    }

    [Function("GetGoals")]
    public async Task<IActionResult> GetGoalsAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/goals")]
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        var correlationId = CorrelationId(request);
        var authorization = Authorize(request, correlationId);
        if (authorization.Error != null)
            return authorization.Error;
        return ToActionResult(
            await ApiService.GetGoalsAsync(authorization.Subject!, cancellationToken),
            correlationId);
    }

    [Function("GetRecords")]
    public async Task<IActionResult> GetRecordsAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/records")]
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        var correlationId = CorrelationId(request);
        var authorization = Authorize(request, correlationId);
        if (authorization.Error != null)
            return authorization.Error;
        return ToActionResult(
            await ApiService.GetRecordsAsync(
                authorization.Subject!,
                request.Query["from"],
                request.Query["to"],
                cancellationToken),
            correlationId);
    }

    [Function("GetMeasurements")]
    public async Task<IActionResult> GetMeasurementsAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/measurements")]
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        var correlationId = CorrelationId(request);
        var authorization = Authorize(request, correlationId);
        if (authorization.Error != null)
            return authorization.Error;
        return ToActionResult(
            await ApiService.GetMeasurementsAsync(
                authorization.Subject!,
                request.Query["from"],
                request.Query["to"],
                cancellationToken),
            correlationId);
    }
}
