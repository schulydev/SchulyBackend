using Mediator;
using Schuly.API.Extensions;
using Schuly.API.Plugins;
using Schuly.API.Services;
using Schuly.Application.Behaviors;
using Schuly.Infrastructure.Services;
using Schuly.Infrastructure.Storage;
using Schuly.Infrastructure.Vault;
using Schuly.Plugin.Abstractions;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

var mvcBuilder = builder.Services.AddSchulyControllers();

builder.Services.AddSchulyOpenApi(builder.Configuration);
builder.Services.AddMediator(options => { options.ServiceLifetime = ServiceLifetime.Scoped; });
builder.Services.AddScoped(typeof(IPipelineBehavior<,>), typeof(AuthorizationBehavior<,>));
builder.Services.AddScoped(typeof(IPipelineBehavior<,>), typeof(PluginEventBehavior<,>));
builder.Services.AddSchulyDatabase(builder.Configuration);

builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<IOidcService, OidcService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddSingleton<IAvatarUrlSigner, AvatarUrlSigner>();
builder.Services.AddScoped<IPluginUserContext, PluginUserContext>();
builder.Services.AddSchulyDocumentStorage(builder.Configuration);

builder.Services.AddSchulyVault();
builder.Services.AddSingleton<PluginSchedulerRegistry>();
builder.Services.AddSchulyPlugins(builder.Configuration, mvcBuilder);

builder.Services.AddSchulyAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddSchulyAuthorization();
builder.Services.AddSchulyExceptionHandling();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 600, Window = TimeSpan.FromMinutes(1) }));
});

if (builder.Environment.IsDevelopment())
    builder.Services.AddSchulyRequestLogging();

var app = builder.Build();

app.ApplyMigrations();
await app.SeedSchoolSystemsAsync();

app.UseExceptionHandler();

app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
    app.UseHttpLogging();
    app.MapSchulyApiReference();
}

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<PluginScopeMiddleware>();
app.MapControllers();
await app.UseSchulyPluginsAsync();

app.Run();
