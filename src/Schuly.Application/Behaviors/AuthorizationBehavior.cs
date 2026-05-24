using Mediator;
using Microsoft.AspNetCore.Http;
using Schuly.Application.Authorization;
using Schuly.Domain.Enums;
using System.Reflection;
using System.Security.Claims;

namespace Schuly.Application.Behaviors
{
    public class AuthorizationBehavior<TRequest, TResponse>(IHttpContextAccessor httpContextAccessor) : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull, IMessage
    {
        public async ValueTask<TResponse> Handle(
            TRequest request,
            MessageHandlerDelegate<TRequest, TResponse> next,
            CancellationToken cancellationToken)
        {
            var type = request.GetType();
            var rolesAttribute = type.GetCustomAttribute<AuthorizedRolesAttribute>();
            var allowAuthenticated = type.GetCustomAttribute<AllowAuthenticatedAttribute>();

            if (rolesAttribute is null && allowAuthenticated is null)
                throw new InvalidOperationException(
                    $"Missing authorization attribute on {type.Name}. Add [AuthorizedRoles(...)] or [AllowAuthenticated].");

            if (allowAuthenticated is not null)
                return await next(request, cancellationToken);

            var currentUserRole = GetCurrentUserRole();
            if (!rolesAttribute!.Roles.Contains(currentUserRole) && currentUserRole != Roles.Administrator)
                throw new UnauthorizedAccessException(
                    $"User with role {currentUserRole} is not authorized to perform this action. Required roles: {string.Join(", ", rolesAttribute.Roles)}.");

            return await next(request, cancellationToken);
        }

        private Roles GetCurrentUserRole()
        {
            var roleClaim = httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Role)?.Value;
            if (roleClaim != null && Enum.TryParse<Roles>(roleClaim, out var role))
                return role;

            return Roles.Student;
        }
    }
}
