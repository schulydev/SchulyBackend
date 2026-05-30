using Schuly.API.Extensions;
using Schuly.API.Services;
using Schuly.Infrastructure.Services;
using Schuly.Infrastructure.Storage;
using Schuly.Plugin.Abstractions;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

var mvcBuilder = builder.Services.AddSchulyControllers();

builder.Services.AddSchulyOpenApi(builder.Configuration);
builder.Services.AddMediator(options => { options.ServiceLifetime = ServiceLifetime.Scoped; });
builder.Services.AddSchulyDatabase(builder.Configuration);

builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<IOidcService, OidcService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddSingleton<IAvatarUrlSigner, AvatarUrlSigner>();
builder.Services.AddScoped<IPluginUserContext, PluginUserContext>();
builder.Services.AddSchulyDocumentStorage(builder.Configuration);

builder.Services.AddPlugins(builder.Configuration, mvcBuilder);
builder.Services.AddSingleton<PluginSchedulerRegistry>();
builder.Services.AddHostedService<PluginBackgroundTaskHost>();

builder.Services.AddSchulyAuthentication(builder.Configuration);
builder.Services.AddSchulyAuthorization();

if (builder.Environment.IsDevelopment())
    builder.Services.AddSchulyRequestLogging();

var app = builder.Build();

app.ApplyMigrations();

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
