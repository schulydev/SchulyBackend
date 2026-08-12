using System.Security.Claims;
using Schuly.Domain.Enums;

namespace Schuly.Domain
{
    public static class ClaimsPrincipalRoleExtensions
    {
        public static bool IsAdministrator(this ClaimsPrincipal? user) =>
            user?.IsInRole(nameof(Roles.Administrator)) ?? false;

        public static bool IsTeacher(this ClaimsPrincipal? user) =>
            user?.IsInRole(nameof(Roles.Teacher)) ?? false;

        public static Roles GetPrimaryRole(this ClaimsPrincipal? user)
        {
            if (user.IsAdministrator()) return Roles.Administrator;
            if (user.IsTeacher()) return Roles.Teacher;
            return Roles.Student;
        }
    }
}
