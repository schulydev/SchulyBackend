using System.Reflection;
using Mediator;
using Microsoft.Extensions.Configuration;
using Schuly.Application.Dtos;
using Schuly.Application.Models;
using Schuly.Application.Authorization;

namespace Schuly.Application.Queries.App
{
    [AllowAuthenticated]
    public record AppQuery() : IQuery<Result<AppDto>>;

    public class AppQueryHandler(IConfiguration configuration) : IQueryHandler<AppQuery, Result<AppDto>>
    {
        private static readonly string Version =
            (Assembly.GetEntryAssembly() ?? typeof(AppQueryHandler).Assembly)
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion
                ?.Split('+')[0]
            ?? "0.0.0";

        public async ValueTask<Result<AppDto>> Handle(AppQuery query, CancellationToken cancellationToken)
        {
            return Result<AppDto>.Success(new AppDto(
                configuration["Oidc:Authority"] ?? string.Empty,
                configuration["Oidc:ClientId"] ?? string.Empty,
                configuration["Oidc:RedirectUri"] ?? "http://localhost:4200/callback",
                configuration["Oidc:PostLogoutRedirectUri"] ?? "http://localhost:4200/",
                configuration["Oidc:Scope"] ?? "openid profile email groups picture offline_access",
                Version
            ));
        }
    }
}
