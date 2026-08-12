using Microsoft.AspNetCore.Http;
using Schuly.Domain;
using Schuly.Infrastructure;
using Schuly.Infrastructure.Services;
using Schuly.Tests.TestHelpers;
using System.Security.Claims;

namespace Schuly.Tests
{
    public class TeacherClassAuthTests
    {
        private const string ExternalId = "ext-teacher";

        private static IHttpContextAccessor Accessor(string? role)
        {
            var ctx = new DefaultHttpContext();
            if (role is not null)
                ctx.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "test"));
            return new HttpContextAccessor { HttpContext = ctx };
        }

        private static UserService NewService(SchulyDbContext ctx, string? role) =>
            new(new FakeOidcService(ExternalId), ctx, Accessor(role));

        private static (Guid taught, Guid other) Seed(SchulyDbContext ctx, bool linkTeacher)
        {
            var schoolId = Guid.NewGuid();
            var user = new ApplicationUser { Id = Guid.NewGuid(), ExternalId = ExternalId, Email = "t@example.com" };

            var taught = new Class { Name = "Taught", SchoolId = schoolId };
            var other = new Class { Name = "Other", SchoolId = schoolId };

            var teacher = new Teacher
            {
                Id = Guid.NewGuid(),
                SchoolId = schoolId,
                FirstName = "T",
                LastName = "Eacher",
                Code = "TEA",
                ApplicationUserId = linkTeacher ? user.Id : null,
                Classes = { taught },
            };

            ctx.ApplicationUsers.Add(user);
            ctx.Classes.AddRange(taught, other);
            ctx.Teachers.Add(teacher);
            ctx.SaveChanges();
            return (taught.Id, other.Id);
        }

        [Test]
        public async Task Linked_teacher_can_manage_a_class_they_teach()
        {
            using var ctx = TestDb.NewContext(nameof(Linked_teacher_can_manage_a_class_they_teach));
            var (taught, _) = Seed(ctx, linkTeacher: true);

            await Assert.That(await NewService(ctx, "Teacher").CanManageClassAsync(taught)).IsTrue();
        }

        [Test]
        public async Task Linked_teacher_cannot_manage_a_class_they_do_not_teach()
        {
            using var ctx = TestDb.NewContext(nameof(Linked_teacher_cannot_manage_a_class_they_do_not_teach));
            var (_, other) = Seed(ctx, linkTeacher: true);

            await Assert.That(await NewService(ctx, "Teacher").CanManageClassAsync(other)).IsFalse();
        }

        [Test]
        public async Task Unlinked_teacher_cannot_manage_any_class()
        {
            using var ctx = TestDb.NewContext(nameof(Unlinked_teacher_cannot_manage_any_class));
            var (_, other) = Seed(ctx, linkTeacher: false);

            await Assert.That(await NewService(ctx, "Teacher").CanManageClassAsync(other)).IsFalse();
        }

        [Test]
        public async Task Administrator_can_manage_any_class()
        {
            using var ctx = TestDb.NewContext(nameof(Administrator_can_manage_any_class));
            var (_, other) = Seed(ctx, linkTeacher: true);

            await Assert.That(await NewService(ctx, "Administrator").CanManageClassAsync(other)).IsTrue();
        }
    }
}
