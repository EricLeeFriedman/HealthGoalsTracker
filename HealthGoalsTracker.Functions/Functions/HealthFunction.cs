using HealthGoalsTracker.Functions.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace HealthGoalsTracker.Functions.Functions;

public class HealthFunction
{
    [Function("Health")]
    public IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/health")]
        HttpRequest request)
    {
        var correlationId = request.Headers["X-Correlation-Id"].ToString();
        if (string.IsNullOrWhiteSpace(correlationId) || correlationId.Length > 128)
            correlationId = Guid.NewGuid().ToString();
        request.HttpContext.Response.Headers["X-Correlation-Id"] = correlationId;

        return new OkObjectResult(new HealthResponse
        {
            ServerTime = DateTimeOffset.UtcNow
        });
    }
}
