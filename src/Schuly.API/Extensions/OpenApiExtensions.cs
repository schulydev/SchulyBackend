using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace Schuly.API.Extensions
{
    public static class OpenApiExtensions
    {
        // OpenAPI 3.0 document generation with the bearer security scheme applied to
        // every operation, so Scalar's Authorize works.
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
                    document.Components ??= new OpenApiComponents();
                    document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                    document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
                    {
                        Type = SecuritySchemeType.Http,
                        Scheme = "bearer",
                        BearerFormat = "JWT"
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
                                [new OpenApiSecuritySchemeReference("Bearer", document)] = []
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
                    .AddPreferredSecuritySchemes("Bearer");
            }).AllowAnonymous();
            return app;
        }
    }
}
