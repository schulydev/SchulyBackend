using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Schuly.API.Extensions;
using Schuly.API.Services;
using Schuly.Infrastructure;
using Schuly.Infrastructure.Services;
using Schuly.Infrastructure.Storage;
using Schuly.Plugin.Abstractions;
using Scalar.AspNetCore;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

var mvcBuilder = builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Required for Swashbuckle to discover Minimal API endpoints (incl. plugin endpoints).
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    var authority = builder.Configuration["Oidc:Authority"]
        ?? throw new InvalidOperationException("Oidc:Authority not configured");

    options.AddSecurityDefinition("OAuth2", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.OAuth2,
        Flows = new OpenApiOAuthFlows
        {
            AuthorizationCode = new OpenApiOAuthFlow
            {
                AuthorizationUrl = new Uri($"{authority}/authorize"),
                TokenUrl = new Uri($"{authority}/api/oidc/token"),
                Scopes = new Dictionary<string, string>
                {
                    ["openid"] = "OpenID Connect",
                    ["profile"] = "User profile",
                    ["email"] = "User email",
                    ["groups"] = "User groups (roles)",
                    ["picture"] = "Profile Picture",
                }
            }
        }
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("OAuth2", document)] = new List<string>()
    });
});

builder.Services.AddMediator(options => { options.ServiceLifetime = ServiceLifetime.Scoped; });

builder.Services.AddDbContext<SchulyDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("SchulyDatabase"),
        npgsqlOptions => npgsqlOptions
            .EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorCodesToAdd: null
            )
    ));

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

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Oidc:Authority"];
        options.RequireHttpsMetadata = builder.Configuration.GetValue("Oidc:RequireHttpsMetadata", true);
        options.TokenValidationParameters.NameClaimType = "name";
        options.TokenValidationParameters.RoleClaimType = "groups";
        options.TokenValidationParameters.ValidateAudience = false;
    })
    .AddUserSync();

builder.Services.AddAuthorization(options =>
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());

// Dev-only HTTP request logging — surfaces method, path, status, and body
// (e.g. 400 reason) for every request. Off in production.
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddHttpLogging(o =>
    {
        o.LoggingFields = Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.RequestPath
                        | Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.RequestMethod
                        | Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.RequestQuery
                        | Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.ResponseStatusCode
                        | Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.ResponseBody;
        o.ResponseBodyLogLimit = 2048;
    });
}

var app = builder.Build();

app.ApplyMigrations();

if (app.Environment.IsDevelopment())
{
    app.UseHttpLogging();

    // Swashbuckle still produces the OpenAPI document at /swagger/v1/swagger.json
    // (the Dart client is generated from it); Scalar renders the interactive UI.
    app.UseSwagger();
    // AllowAnonymous: the app's global RequireAuthenticatedUser fallback policy would
    // otherwise 401 the Scalar reference endpoint (the static assets are already
    // exempt internally).
    app.MapScalarApiReference(options =>
    {
        options
            .WithTitle("Schuly API")
            // Swashbuckle serves the doc at /swagger/v1/swagger.json; register it
            // so Scalar maps the reference page at /scalar (and /scalar/v1).
            .AddDocument("v1", routePattern: "/swagger/{documentName}/swagger.json")
            .AddPreferredSecuritySchemes("OAuth2")
            .AddAuthorizationCodeFlow("OAuth2", flow =>
            {
                flow.ClientId = builder.Configuration["Oidc:ClientId"];
                flow.Pkce = Pkce.Sha256;
                flow.SelectedScopes = ["openid", "profile", "email", "groups", "picture"];
            });
    }).AllowAnonymous();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
await app.UsePluginsAsync();

app.Run();
