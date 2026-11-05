using System.Text.Json.Serialization;
using DemoTaskManager.Api.Infrastructure;
using DemoTaskManager.Infrastructure;
using DemoTaskManager.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Net.Http.Headers;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.Configure<ApiBehaviorOptions>(options => options.SuppressMapClientErrors = false);

builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = context =>
        context.ProblemDetails.Extensions["correlationId"] = context.HttpContext.GetCorrelationId());

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => options.SwaggerDoc("v1", new()
{
    Title = "Demo Task Manager API",
    Version = "v1",
    Description = "Task management demo API backed by EF Core and Azure Service Bus."
}));

builder.Services.AddInfrastructureServices(builder.Configuration);

// Application Insights throws when it is registered without a connection string, so the
// telemetry pipeline is opt-in through configuration.
if (!string.IsNullOrWhiteSpace(builder.Configuration["ApplicationInsights:ConnectionString"])
    || !string.IsNullOrWhiteSpace(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
{
    builder.Services.AddApplicationInsightsTelemetry();
}

builder.Services.AddResponseCompression(options => options.EnableForHttps = false);

var healthChecks = builder.Services.AddHealthChecks();
var connectionString = builder.Configuration.GetConnectionString(InfrastructureServiceCollectionExtensions.DefaultConnectionName);
if (!string.IsNullOrWhiteSpace(connectionString))
{
    healthChecks.AddSqlServer(connectionString, name: "sql", tags: ["ready"]);
}

// App Service terminates TLS at the front end; honour the forwarded scheme so redirects work.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

const string DevCorsPolicy = "AngularDevServer";
builder.Services.AddCors(options => options.AddPolicy(DevCorsPolicy, policy => policy
    .WithOrigins("http://localhost:4200", "https://localhost:4200")
    .AllowAnyHeader()
    .AllowAnyMethod()
    .WithExposedHeaders(CorrelationIdMiddleware.HeaderName)));

var app = builder.Build();

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseResponseCompression();

if (app.Environment.IsDevelopment())
{
    app.UseCors(DevCorsPolicy);
    app.UseSwagger();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "Demo Task Manager API v1"));
}
else
{
    app.UseHsts();
}

app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        // Angular emits content-hashed asset file names, so everything except index.html
        // can be cached aggressively.
        var isIndex = ctx.File.Name.Equals("index.html", StringComparison.OrdinalIgnoreCase);
        ctx.Context.Response.Headers[HeaderNames.CacheControl] =
            isIndex ? "no-cache, no-store, must-revalidate" : "public, max-age=31536000, immutable";
    }
});

app.MapControllers();

// Runtime configuration for the SPA: lets the API base URL change per environment
// without rebuilding the Angular bundle.
app.MapGet("/config.json", (IConfiguration configuration, IWebHostEnvironment env) => Results.Ok(new
{
    apiBaseUrl = configuration["Spa:ApiBaseUrl"] ?? "/api",
    environmentName = env.EnvironmentName,
    applicationInsightsConnectionString = configuration["Spa:ApplicationInsightsConnectionString"]
})).ExcludeFromDescription();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = HealthResponseWriter.WriteAsync
});

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = HealthResponseWriter.WriteAsync
});

// SPA fallback, explicitly disabled for API and health routes so a bad /api path
// returns 404 JSON instead of the Angular shell.
app.MapFallback(async context =>
{
    var path = context.Request.Path;
    if (path.StartsWithSegments("/api")
        || path.StartsWithSegments("/health")
        || path.StartsWithSegments("/swagger"))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    var indexPath = Path.Combine(app.Environment.WebRootPath ?? "wwwroot", "index.html");
    if (!File.Exists(indexPath))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        await context.Response.WriteAsync(
            "Angular build output was not found. Run scripts/build.ps1 or npm run build in src/DemoTaskManager.Web.")
            .ConfigureAwait(false);
        return;
    }

    context.Response.ContentType = "text/html; charset=utf-8";
    context.Response.Headers[HeaderNames.CacheControl] = "no-cache, no-store, must-revalidate";
    await context.Response.SendFileAsync(indexPath).ConfigureAwait(false);
});

if (app.Environment.IsDevelopment() && app.Configuration.GetValue("Database:AutoMigrate", true))
{
    await DbInitializer.MigrateAndSeedAsync(app.Services, CancellationToken.None).ConfigureAwait(false);
}

await app.RunAsync().ConfigureAwait(false);

/// <summary>Exposed so <c>WebApplicationFactory&lt;Program&gt;</c> can host the API in tests.</summary>
public partial class Program;
