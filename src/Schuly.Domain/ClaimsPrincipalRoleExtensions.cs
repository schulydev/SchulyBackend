using System.Security.Claims;
using Schuly.Domain.Enums;

namespace Schuly.Domain
{
    /// <summary>
    /// Resolves Schuly roles from the authenticated principal via the role claim
    /// configured on the JWT bearer handler (RoleClaimType). Using IsInRole means
    /// this works for both the production "groups" claim and the dev ClaimTypes.Role
    /// claim without hardcoding either claim type - the single source of truth for
    /// "what role is the caller".
    /// </summary>
    public static class ClaimsPrincipalRoleExtensions
    {
        public static bool IsAdministrator(this ClaimsPrincipal? user) =>
            user?.IsInRole(nameof(Roles.Administrator)) ?? false;

        public static bool IsTeacher(this ClaimsPrincipal? user) =>
            user?.IsInRole(nameof(Roles.Teacher)) ?? false;

        /// <summary>The highest-privilege role the user holds (Administrator &gt; Teacher &gt; Student).</summary>
        public static Roles GetPrimaryRole(this ClaimsPrincipal? user)
        {
            if (user.IsAdministrator()) return Roles.Administrator;
            if (user.IsTeacher()) return Roles.Teacher;
            return Roles.Student;
        }
    }
}
