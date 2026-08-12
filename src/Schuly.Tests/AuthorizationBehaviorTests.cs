using Mediator;
using Microsoft.AspNetCore.Http;
using Schuly.Application.Authorization;
using Schuly.Application.Behaviors;
using Schuly.Domain.Enums;
using System.Linq;
using System.Security.Claims;

namespace Schuly.Tests
{
    public class AuthorizationBehaviorTests
    {
        [AuthorizedRoles(Roles.Teacher)]
        private sealed record TeacherOnlyMessage : IMessage;

        [AllowAuthenticated]
        private sealed record AnyAuthenticatedMessage : IMessage;

        private sealed record NoAttributeMessage : IMessage;

        private static IHttpContextAccessor AccessorWithRole(string? role)
        {
            var ctx = new DefaultHttpContext();
            if (role is not null)
                ctx.User = new ClaimsPrincipal(
                    new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "test"));
            return new HttpContextAccessor { HttpContext = ctx };
        }

        private static IHttpContextAccessor AccessorWithGroups(params string[] groups)
        {
            var ctx = new DefaultHttpContext();
            var identity = new ClaimsIdentity(
                groups.Select(g => new Claim("groups", g)),
                authenticationType: "test",
                nameType: "name",
                roleType: "groups");
            ctx.User = new ClaimsPrincipal(identity);
            return new HttpContextAccessor { HttpContext = ctx };
        }

        private static ValueTask<string> Next<T>(T _, CancellationToken __) where T : notnull, IMessage
            => ValueTask.FromResult("ok");

        [Test]
        public async Task Student_is_denied_on_a_teacher_only_message()
        {
            var behavior = new AuthorizationBehavior<TeacherOnlyMessage, string>(AccessorWithRole("Student"));

            await Assert.That(async () =>
                    await behavior.Handle(new TeacherOnlyMessage(), Next, CancellationToken.None))
                .Throws<UnauthorizedAccessException>();
        }

        [Test]
        public async Task Missing_role_claim_defaults_to_student_and_is_denied()
        {
            var behavior = new AuthorizationBehavior<TeacherOnlyMessage, string>(AccessorWithRole(null));

            await Assert.That(async () =>
                    await behavior.Handle(new TeacherOnlyMessage(), Next, CancellationToken.None))
                .Throws<UnauthorizedAccessException>();
        }

        [Test]
        public async Task Teacher_is_allowed_on_a_teacher_only_message()
        {
            var behavior = new AuthorizationBehavior<TeacherOnlyMessage, string>(AccessorWithRole("Teacher"));

            var result = await behavior.Handle(new TeacherOnlyMessage(), Next, CancellationToken.None);

            await Assert.That(result).IsEqualTo("ok");
        }

        [Test]
        public async Task Administrator_bypasses_the_role_gate()
        {
            var behavior = new AuthorizationBehavior<TeacherOnlyMessage, string>(AccessorWithRole("Administrator"));

            var result = await behavior.Handle(new TeacherOnlyMessage(), Next, CancellationToken.None);

            await Assert.That(result).IsEqualTo("ok");
        }

        [Test]
        public async Task AllowAuthenticated_passes_regardless_of_role()
        {
            var behavior = new AuthorizationBehavior<AnyAuthenticatedMessage, string>(AccessorWithRole("Student"));

            var result = await behavior.Handle(new AnyAuthenticatedMessage(), Next, CancellationToken.None);

            await Assert.That(result).IsEqualTo("ok");
        }

        [Test]
        public async Task Message_without_an_authorization_attribute_throws()
        {
            var behavior = new AuthorizationBehavior<NoAttributeMessage, string>(AccessorWithRole("Administrator"));

            await Assert.That(async () =>
                    await behavior.Handle(new NoAttributeMessage(), Next, CancellationToken.None))
                .Throws<InvalidOperationException>();
        }

        [Test]
        public async Task Teacher_via_production_groups_claim_is_allowed()
        {
            var behavior = new AuthorizationBehavior<TeacherOnlyMessage, string>(AccessorWithGroups("Teacher"));

            var result = await behavior.Handle(new TeacherOnlyMessage(), Next, CancellationToken.None);

            await Assert.That(result).IsEqualTo("ok");
        }

        [Test]
        public async Task Student_via_production_groups_claim_is_denied()
        {
            var behavior = new AuthorizationBehavior<TeacherOnlyMessage, string>(AccessorWithGroups("Student"));

            await Assert.That(async () =>
                    await behavior.Handle(new TeacherOnlyMessage(), Next, CancellationToken.None))
                .Throws<UnauthorizedAccessException>();
        }

        [Test]
        public async Task Administrator_via_production_groups_claim_bypasses_the_role_gate()
        {
            var behavior = new AuthorizationBehavior<TeacherOnlyMessage, string>(AccessorWithGroups("Administrator"));

            var result = await behavior.Handle(new TeacherOnlyMessage(), Next, CancellationToken.None);

            await Assert.That(result).IsEqualTo("ok");
        }
    }
}
