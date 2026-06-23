using Mediator;
using Schuly.API.Extensions;
using Schuly.API.Services;
using Schuly.Application.Behaviors;
using Schuly.Infrastructure.Services;
using Schuly.Infrastructure.Storage;
using Schuly.Infrastructure.Vault;
using Schuly.Plugin.Abstractions;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

var mvcBuilder = builder.Services.AddSchulyControllers();

builder.Services.AddSchulyOpenApi(builder.Configuration);
builder.Services.AddMediator(options => { options.ServiceLifetime = ServiceLifetime.Scoped; });
// Mediator does not auto-register pipeline behaviors — they must be added explicitly,
// and run in registration order. Authorization first so role gates are enforced.
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
builder.Services.AddPlugins(builder.Configuration, mvcBuilder);
builder.Services.AddSingleton<PluginSchedulerRegistry>();
builder.Services.AddHostedService<PluginBackgroundTaskHost>();

builder.Services.AddSchulyAuthentication(builder.Configuration);
builder.Services.AddSchulyAuthorization();
builder.Services.AddSchulyExceptionHandling();

if (builder.Environment.IsDevelopment())
    builder.Services.AddSchulyRequestLogging();

var app = builder.Build();

app.ApplyMigrations();
await app.SeedSchoolSystemsAsync();

app.UseExceptionHandler();

// Serve static assets (e.g. school-system logos under wwwroot/schoolsystems)
// anonymously, before auth, so the app's catalog picker can load them.
app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
    app.UseHttpLogging();
    app.MapSchulyApiReference();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
await app.UsePluginsAsync();

app.Run();
