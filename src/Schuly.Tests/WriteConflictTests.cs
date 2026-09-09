using Microsoft.EntityFrameworkCore;
using Schuly.Application.Commands.Class;
using Schuly.Application.Commands.SchoolUser;
using Schuly.Application.Commands.Teacher;
using Schuly.Application.Models;
using Schuly.Domain;
using Schuly.Domain.Enums;
using Schuly.Tests.TestHelpers;

namespace Schuly.Tests
{
    public class WriteConflictTests
    {
        [Test]
        public async Task Enrolling_the_same_student_twice_returns_conflict_and_leaves_one_roster_entry()
        {
            var dbName = nameof(Enrolling_the_same_student_twice_returns_conflict_and_leaves_one_roster_entry);
            var schoolId = Guid.NewGuid();
            var student = TestDb.NewSchoolUser(schoolId, "Alice");
            var classEntity = new Class { Name = "Class 1", SchoolId = schoolId };

            using (var seedCtx = TestDb.NewContext(dbName))
            {
                seedCtx.SchoolUsers.Add(student);
                seedCtx.Classes.Add(classEntity);
                seedCtx.SaveChanges();
            }

            using (var ctx = TestDb.NewContext(dbName))
            {
                var handler = new EnrolStudentCommandHandler(ctx, new FakeUserService(true));
                var result = await handler.Handle(new EnrolStudentCommand(student.Id, classEntity.Id), CancellationToken.None);
                await Assert.That(result.IsSuccess).IsTrue();
            }

            using (var ctx = TestDb.NewContext(dbName))
            {
                var handler = new EnrolStudentCommandHandler(ctx, new FakeUserService(true));
                var result = await handler.Handle(new EnrolStudentCommand(student.Id, classEntity.Id), CancellationToken.None);
                await Assert.That(result.Status).IsEqualTo(ResultStatus.Conflict);
            }

            using (var ctx = TestDb.NewContext(dbName))
            {
                var roster = ctx.Classes.Include(c => c.Students).Single(c => c.Id == classEntity.Id);
                await Assert.That(roster.Students.Count).IsEqualTo(1);
            }
        }

        [Test]
        public async Task Creating_a_class_with_a_duplicate_name_returns_conflict()
        {
            var dbName = nameof(Creating_a_class_with_a_duplicate_name_returns_conflict);
            var schoolId = Guid.NewGuid();

            using (var seedCtx = TestDb.NewContext(dbName))
            {
                seedCtx.Schools.Add(new School { Id = schoolId, Name = "Test School" });
                seedCtx.SaveChanges();
            }

            using (var ctx = TestDb.NewContext(dbName))
            {
                var handler = new CreateClassCommandHandler(ctx, new FakeUserService(true));
                var result = await handler.Handle(new CreateClassCommand("Math", null, schoolId), CancellationToken.None);
                await Assert.That(result.IsSuccess).IsTrue();
            }

            using (var ctx = TestDb.NewContext(dbName))
            {
                var handler = new CreateClassCommandHandler(ctx, new FakeUserService(true));
                var result = await handler.Handle(new CreateClassCommand("Math", null, schoolId), CancellationToken.None);
                await Assert.That(result.Status).IsEqualTo(ResultStatus.Conflict);
            }
        }

        [Test]
        public async Task Creating_a_teacher_with_an_unknown_school_returns_failure()
        {
            using var ctx = TestDb.NewContext(nameof(Creating_a_teacher_with_an_unknown_school_returns_failure));

            var handler = new CreateTeacherCommandHandler(ctx);
            var result = await handler.Handle(new CreateTeacherCommand(Guid.NewGuid(), "Jane", "Doe", "JD", null), CancellationToken.None);

            await Assert.That(result.Status).IsEqualTo(ResultStatus.Error);
        }

        [Test]
        public async Task Creating_a_school_user_with_an_unknown_application_user_returns_failure()
        {
            var schoolId = Guid.NewGuid();
            using var ctx = TestDb.NewContext(nameof(Creating_a_school_user_with_an_unknown_application_user_returns_failure));
            ctx.Schools.Add(new School { Id = schoolId, Name = "Test School" });
            ctx.SaveChanges();

            var handler = new CreateSchoolUserCommandHandler(ctx);
            var result = await handler.Handle(
                new CreateSchoolUserCommand(Guid.NewGuid(), schoolId, "Jane", "Doe", "jane@example.com", null, null, null, null, null, new DateOnly(2000, 1, 1), new DateOnly(2020, 1, 1), Roles.Student),
                CancellationToken.None);

            await Assert.That(result.Status).IsEqualTo(ResultStatus.Error);
        }
    }
}
