using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace Schuly.API.Extensions
{
    public static class OpenApiExtensions
    {
        private static readonly string[] Scopes = ["openid", "profile", "email", "groups", "picture"];

        // OpenAPI 3.0 document generation with the OAuth2 (authorization-code + PKCE)
        // security scheme applied to every operation, so Scalar's Authorize works.
        // 3.0 (not 3.1) because the app's dart-dio client generator can't parse the
        // JSON Schema 2020-12 constructs that OpenAPI 3.1 emits.
        public static IServiceCollection AddSchulyOpenApi(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddEndpointsApiExplorer();
            services.AddOpenApi(options =>
            {
                options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_0;

                // .NET 10's OpenAPI emits numeric types with a format + pattern but no
                // "type" keyword, which makes the dart-dio generator fall back to a
                // freeform JsonObject. Restore the explicit type so the client gets num/int.
                options.AddSchemaTransformer((schema, context, cancellationToken) =>
                {
                    var type = Nullable.GetUnderlyingType(context.JsonTypeInfo.Type) ?? context.JsonTypeInfo.Type;

                    // Preserve any existing nullability (the Null flag → "nullable": true).
                    var nullFlag = (schema.Type ?? default) & JsonSchemaType.Null;

                    if (type == typeof(decimal) || type == typeof(double) || type == typeof(float))
                        schema.Type = JsonSchemaType.Number | nullFlag;
                    else if (type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte))
                        schema.Type = JsonSchemaType.Integer | nullFlag;
                    return Task.CompletedTask;
                });
                options.AddDocumentTransformer((document, context, cancellationToken) =>
                {
                    var authority = configuration["Oidc:Authority"]
                        ?? throw new InvalidOperationException("Oidc:Authority not configured");

                    document.Components ??= new OpenApiComponents();
                    document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                    document.Components.SecuritySchemes["OAuth2"] = new OpenApiSecurityScheme
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
                    };

                    foreach (var pathItem in document.Paths.Values)
                    {
                        if (pathItem.Operations is null)
                            continue;

                        foreach (var operation in pathItem.Operations.Values)
                        {
                            operation.Security ??= [];
                            operation.Security.Add(new OpenApiSecurityRequirement
                            {
                                [new OpenApiSecuritySchemeReference("OAuth2", document)] = []
                            });
                        }
                    }

                    return Task.CompletedTask;
                });
            });
            return services;
        }

        // Serves the OpenAPI document at /openapi/v1.json and the Scalar API reference
        // at /scalar. Both are AllowAnonymous so the global RequireAuthenticatedUser
        // fallback policy doesn't 401 them (Scalar + the Dart generator fetch the spec).
        public static WebApplication MapSchulyApiReference(this WebApplication app)
        {
            app.MapOpenApi().AllowAnonymous();
            app.MapScalarApiReference(options =>
            {
                options
                    .WithTitle("Schuly API")
                    .AddDocument("v1", routePattern: "/openapi/{documentName}.json")
                    .AddPreferredSecuritySchemes("OAuth2")
                    .AddAuthorizationCodeFlow("OAuth2", flow =>
                    {
                        flow.ClientId = app.Configuration["Oidc:ClientId"];
                        flow.Pkce = Pkce.Sha256;
                        flow.SelectedScopes = Scopes;
                    });
            }).AllowAnonymous();
            return app;
        }
    }
}
