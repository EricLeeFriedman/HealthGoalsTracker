using HealthGoalsTracker.Functions.Contracts;
using HealthGoalsTracker.Functions.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace HealthGoalsTracker.Functions.Functions;

public class ApiFunctionBase
{
    public RequestIdentityResolver IdentityResolver { get; }

    public ApiFunctionBase(RequestIdentityResolver identityResolver)
    {
        IdentityResolver = identityResolver;
    }

    public string CorrelationId(HttpRequest request)
    {
        var supplied = request.Headers["X-Correlation-Id"].ToString();
        var correlationId = !string.IsNullOrWhiteSpace(supplied) && supplied.Length <= 128
            ? supplied
            : Guid.NewGuid().ToString();
        request.HttpContext.Response.Headers["X-Correlation-Id"] = correlationId;
        return correlationId;
    }

    public (string? Subject, IActionResult? Error) Authorize(
        HttpRequest request,
        string correlationId)
    {
        var identity = IdentityResolver.Resolve(request);
        if (identity.Subject != null)
            return (identity.Subject, null);

        var status = identity.IsForbidden
            ? StatusCodes.Status403Forbidden
            : StatusCodes.Status401Unauthorized;
        var code = identity.IsForbidden ? "forbidden" : "unauthorized";
        var message = identity.IsForbidden
            ? "The identity does not have the required delegated scope."
            : "A validated user identity is required.";
        return (null, new ObjectResult(new ApiError
            {
                Code = code,
                Message = message,
                CorrelationId = correlationId
            }) { StatusCode = status });
    }

    public static IActionResult ToActionResult<T>(
        ApiOperationResult<T> result,
        string correlationId) =>
        result.IsValid
            ? new OkObjectResult(result.Value)
            : new BadRequestObjectResult(new ApiError
            {
                Code = "validation_failed",
                Message = "One or more request values are invalid.",
                CorrelationId = correlationId,
                Details = result.ValidationErrors
            });
}
