using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Schuly.Infrastructure.Services;
using System.Security.Claims;

namespace Schuly.API.Extensions
{
    public static class AuthenticationExtensions
    {
        // JWT bearer auth against the configured OIDC authority, plus user-sync on
        // first valid token. In Development, an opt-in fake-OIDC path validates
        // locally minted tokens instead (see DevAuthDefaults / DevAuthController).
        public static IServiceCollection AddSchulyAuthentication(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    if (DevAuthDefaults.IsEnabled(configuration))
                    {
                        // Local testing only: trust tokens minted by /api/dev/token,
                        // signed with a symmetric key. No external IdP is contacted.
                        options.RequireHttpsMetadata = false;
                        options.MapInboundClaims = false;
                        options.TokenValidationParameters = new TokenValidationParameters
                        {
                            ValidateIssuer = true,
                            ValidIssuer = DevAuthDefaults.Issuer(configuration),
                            ValidateAudience = false,
                            ValidateIssuerSigningKey = true,
                            IssuerSigningKey = DevAuthDefaults.SigningKey(configuration),
                            NameClaimType = "name",
                            RoleClaimType = ClaimTypes.Role,
                        };
                    }
                    else
                    {
                        options.Authority = configuration["Oidc:Authority"];
                        options.RequireHttpsMetadata = configuration.GetValue("Oidc:RequireHttpsMetadata", true);
                        options.TokenValidationParameters.NameClaimType = "name";
                        options.TokenValidationParameters.RoleClaimType = "groups";
                        options.TokenValidationParameters.ValidateAudience = false;
                    }
                })
                .AddUserSync();
            return services;
        }

        // Default policy: every endpoint requires an authenticated user unless it
        // opts out with [AllowAnonymous].
        public static IServiceCollection AddSchulyAuthorization(this IServiceCollection services)
        {
            services.AddAuthorization(options =>
                options.FallbackPolicy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build());
            return services;
        }

        public static AuthenticationBuilder AddUserSync(this AuthenticationBuilder builder)
        {
            builder.Services.PostConfigure<JwtBearerOptions>(
                JwtBearerDefaults.AuthenticationScheme,
                options =>
                {
                    options.Events ??= new JwtBearerEvents();
                    var previous = options.Events.OnTokenValidated;

                    options.Events.OnTokenValidated = async context =>
                    {
                        if (previous is not null)
                            await previous(context);

                        if (context.Principal?.Identity is not ClaimsIdentity identity)
                            return;

                        context.HttpContext.User = context.Principal;

                        var externalId = identity.FindFirst("sub")?.Value ?? identity.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                        if (string.IsNullOrEmpty(externalId))
                            return;

                        var userService = context.HttpContext.RequestServices.GetRequiredService<IUserService>();

                        if (await userService.ExistsAsync(externalId, context.HttpContext.RequestAborted))
                            return;

                        await userService.SyncCurrentUserAsync(context.HttpContext.RequestAborted);
                    };
                });

            return builder;
        }
    }
}
