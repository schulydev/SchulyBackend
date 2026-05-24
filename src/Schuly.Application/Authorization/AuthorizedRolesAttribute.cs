using Schuly.Domain.Enums;

namespace Schuly.Application.Authorization
{
    [AttributeUsage(AttributeTargets.Class)]
    public class AuthorizedRolesAttribute(params Roles[] roles) : Attribute
    {
        public Roles[] Roles { get; } = roles;
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class AllowAuthenticatedAttribute : Attribute;
}
