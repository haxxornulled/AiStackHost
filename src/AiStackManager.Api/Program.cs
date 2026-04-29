using AiStackManager.Api.Composition;
using AiStackManager.Api.Endpoints;
using AiStackManager.Api.Security;
using Autofac.Extensions.DependencyInjection;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services.AddControllers().AddNewtonsoftJson();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHealthChecks();
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("AiStackManager"))
    .WithTracing(t => t.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation());

builder.RegisterAiStack();

var app = builder.Build();

app.MapHealthChecks("/health");
app.UseMiddleware<ManagementApiGuard>();
app.MapAiStackMinimalEndpoints();
app.MapControllers();

app.Run();

// Expose a Program type for WebApplicationFactory-based integration tests
public partial class Program { }
