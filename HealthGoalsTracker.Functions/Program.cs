using HealthGoalsTracker.Functions.Services;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();
builder.Services.AddSingleton(provider =>
{
    var configuration = provider.GetRequiredService<IConfiguration>();
    var signingKey = configuration["CursorSigningKey"] ??
        throw new InvalidOperationException("CursorSigningKey is required.");
    return new CursorCodec(signingKey);
});
builder.Services.AddSingleton<ICloudRepository, InMemoryCloudRepository>();
builder.Services.AddSingleton<CloudApiService>();
builder.Services.AddSingleton<RequestIdentityResolver>();

builder.Build().Run();
