using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace HealthGoalsTracker.Functions.Services;

public class RequestIdentityResolver
{
    public IConfiguration Configuration { get; }

    public RequestIdentityResolver(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public IdentityResolution Resolve(HttpRequest request)
    {
        var claims = request.HttpContext.User.Claims.ToList();
        if (claims.Count == 0 &&
            Configuration.GetValue<bool>("WEBSITE_AUTH_ENABLED"))
        {
            if (!TryReadEasyAuthClaims(request, out claims))
                return IdentityResolution.Unauthorized();
        }

        var subject = ClaimValue(claims, "sub", ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(subject))
        {
            var requiredScope = Configuration["ApiScope"];
            var scopeValue = ClaimValue(
                claims,
                "scp",
                "http://schemas.microsoft.com/identity/claims/scope");
            var scopes = scopeValue?
                .Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [];
            return string.IsNullOrWhiteSpace(requiredScope) || !scopes.Contains(requiredScope)
                ? IdentityResolution.Forbidden()
                : IdentityResolution.Success(subject);
        }

        if (!Configuration.GetValue<bool>("AllowDevelopmentIdentity") ||
            Configuration["AZURE_FUNCTIONS_ENVIRONMENT"] != "Development")
            return IdentityResolution.Unauthorized();

        var developmentSubject = request.Headers["X-HealthGoals-Test-Subject"].ToString();
        return string.IsNullOrWhiteSpace(developmentSubject) ||
               developmentSubject.Length > 128
            ? IdentityResolution.Unauthorized()
            : IdentityResolution.Success(developmentSubject);
    }

    public static string? ClaimValue(
        IEnumerable<Claim> claims,
        params string[] claimTypes) =>
        claims.FirstOrDefault(claim => claimTypes.Contains(claim.Type, StringComparer.Ordinal))
            ?.Value;

    public static bool TryReadEasyAuthClaims(
        HttpRequest request,
        out List<Claim> claims)
    {
        claims = [];
        var encodedPrincipal = request.Headers["X-MS-CLIENT-PRINCIPAL"].ToString();
        if (string.IsNullOrWhiteSpace(encodedPrincipal))
            return false;
        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(encodedPrincipal));
            var principal = JsonSerializer.Deserialize<EasyAuthPrincipal>(
                json,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
            if (principal?.Claims is null)
                return false;
            claims = principal.Claims
                .Where(claim => !string.IsNullOrWhiteSpace(claim.Type))
                .Select(claim => new Claim(claim.Type, claim.Value ?? ""))
                .ToList();
            return claims.Count > 0;
        }
        catch (Exception exception) when (
            exception is FormatException or JsonException)
        {
            return false;
        }
    }
}

public class IdentityResolution
{
    public string? Subject { get; set; }
    public bool IsForbidden { get; set; }

    public static IdentityResolution Success(string subject) => new() { Subject = subject };
    public static IdentityResolution Unauthorized() => new();
    public static IdentityResolution Forbidden() => new() { IsForbidden = true };
}

public class EasyAuthPrincipal
{
    public List<EasyAuthClaim>? Claims { get; set; }
}

public class EasyAuthClaim
{
    [System.Text.Json.Serialization.JsonPropertyName("typ")]
    public string Type { get; set; } = "";
    [System.Text.Json.Serialization.JsonPropertyName("val")]
    public string? Value { get; set; }
}
