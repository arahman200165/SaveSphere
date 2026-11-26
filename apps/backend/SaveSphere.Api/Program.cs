using Serilog;
using Serilog.Core;
using SaveSphere.Api;
using SaveSphere.Api.Health;
using SaveSphere.Infrastructure.Endpoints;
using SaveSphere.Infrastructure.Logging;
using SaveSphere.Modules.Core;

if (args.Contains("--healthcheck", StringComparer.Ordinal))
{
    return await HealthProbe.RunAsync();
}

var builder = WebApplication.CreateBuilder(args);

// preserveStaticLogger: the host owns its logger; nothing depends on the process-wide Log.Logger, so several
// hosts (for example in parallel integration tests) never share or steal each other's log events.
builder.Host.UseSerilog(
    (_, services, configuration) => configuration.ConfigureSaveSphere(services.GetServices<ILogEventSink>()),
    preserveStaticLogger: true);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info.Title = "SaveSphere API";
        document.Info.Version = "v1";
        document.Servers?.Clear();
        return Task.CompletedTask;
    });
});

builder.Services.AddCoreModule();
builder.Services.AddSingleton<IEndpointModule, HealthEndpoints>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSerilogRequestLogging(options => options.Logger = app.Services.GetRequiredService<Serilog.ILogger>());

app.MapOpenApi("/api/v1/openapi/{documentName}.json");

var api = app.MapGroup("/api/v1");
foreach (var module in app.Services.GetServices<IEndpointModule>())
{
    module.MapEndpoints(api);
}

await app.RunAsync();
return 0;

#pragma warning disable CA1050, S1118 // Declare types in namespaces: required for WebApplicationFactory<Program>.
public partial class Program;
#pragma warning restore CA1050, S1118
