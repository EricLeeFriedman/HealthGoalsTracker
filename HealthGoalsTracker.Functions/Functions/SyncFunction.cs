using System.Text.Json;
using HealthGoalsTracker.Functions.Contracts;
using HealthGoalsTracker.Functions.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace HealthGoalsTracker.Functions.Functions;

public class SyncFunction : ApiFunctionBase
{
    public CloudApiService ApiService { get; }
    public ILogger<SyncFunction> Logger { get; }

    public SyncFunction(
        CloudApiService apiService,
        RequestIdentityResolver identityResolver,
        ILogger<SyncFunction> logger)
        : base(identityResolver)
    {
        ApiService = apiService;
        Logger = logger;
    }

    [Function("Sync")]
    public async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/sync")]
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        var correlationId = CorrelationId(request);
        var authorization = Authorize(request, correlationId);
        if (authorization.Error != null)
            return authorization.Error;

        SyncRequest? syncRequest;
        try
        {
            syncRequest = await JsonSerializer.DeserializeAsync<SyncRequest>(
                request.Body,
                new JsonSerializerOptions(JsonSerializerDefaults.Web),
                cancellationToken);
        }
        catch (JsonException)
        {
            return new BadRequestObjectResult(new ApiError
            {
                Code = "invalid_json",
                Message = "Request body must contain valid JSON.",
                CorrelationId = correlationId
            });
        }

        if (syncRequest == null)
        {
            return new BadRequestObjectResult(new ApiError
            {
                Code = "invalid_json",
                Message = "Request body is required.",
                CorrelationId = correlationId
            });
        }

        var nullErrors = ValidateNullCollections(syncRequest);
        if (nullErrors.Count > 0)
        {
            return new BadRequestObjectResult(new ApiError
            {
                Code = "validation_failed",
                Message = "Request validation failed.",
                CorrelationId = correlationId,
                Details = nullErrors
            });
        }

        Logger.LogInformation(
            "Sync requested: goals {GoalCount}, records {RecordCount}, measurements {MeasurementCount}, correlation {CorrelationId}",
            syncRequest.Goals.Count,
            syncRequest.DailyRecords.Count,
            syncRequest.Measurements.Count,
            correlationId);
        var result = await ApiService.SyncAsync(
            authorization.Subject!,
            syncRequest,
            cancellationToken);
        return ToActionResult(result, correlationId);
    }

    public static Dictionary<string, string[]> ValidateNullCollections(SyncRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.Goals is null)
            errors["goals"] = ["Goals must be an array, not null."];
        else
        {
            for (var index = 0; index < request.Goals.Count; index++)
            {
                if (request.Goals[index] is null)
                    errors[$"goals[{index}]"] = ["Goal must not be null."];
            }
        }
        if (request.DailyRecords is null)
            errors["dailyRecords"] = ["DailyRecords must be an array, not null."];
        if (request.Measurements is null)
            errors["measurements"] = ["Measurements must be an array, not null."];
        else
        {
            for (var index = 0; index < request.Measurements.Count; index++)
            {
                if (request.Measurements[index] is null)
                    errors[$"measurements[{index}]"] = ["Measurement must not be null."];
            }
        }
        if (request.DailyRecords is not null)
        {
            for (var index = 0; index < request.DailyRecords.Count; index++)
            {
                if (request.DailyRecords[index] is null)
                    errors[$"dailyRecords[{index}]"] = ["Daily record must not be null."];
                else if (request.DailyRecords[index].Entries is null)
                    errors[$"dailyRecords[{index}].entries"] = ["Entries must be an array, not null."];
                else
                {
                    for (var entryIndex = 0;
                         entryIndex < request.DailyRecords[index].Entries.Count;
                         entryIndex++)
                    {
                        if (request.DailyRecords[index].Entries[entryIndex] is null)
                        {
                            errors[$"dailyRecords[{index}].entries[{entryIndex}]"] =
                                ["Entry must not be null."];
                        }
                    }
                }
            }
        }
        return errors;
    }
}
