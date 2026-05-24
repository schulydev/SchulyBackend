using Schuly.Domain.Enums;

namespace Schuly.Application.Contracts.Authorization
{
    public interface IHasAuthorization
    {
        Roles GetRequiredRole();
    }
}
