using Schuly.Application.Commands.School;
using Schuly.Application.Models;
using Schuly.Domain;
using Schuly.Tests.TestHelpers;

namespace Schuly.Tests
{
    public class DeleteSchoolDependentsTests
    {
        private static School NewSchool() => new() { Id = Guid.NewGuid(), Name = "Test School" };

        [Test]
        public async Task A_school_with_only_teachers_cannot_be_deleted()
        {
            using var ctx = TestDb.NewContext(nameof(A_school_with_only_teachers_cannot_be_deleted));
            var school = NewSchool();
            ctx.Schools.Add(school);
            ctx.Teachers.Add(new Teacher { SchoolId = school.Id, FirstName = "T", LastName = "X", Code = "TX" });
            await ctx.SaveChangesAsync();

            var result = await new DeleteSchoolCommandHandler(ctx).Handle(new DeleteSchoolCommand(school.Id), CancellationToken.None);

            await Assert.That(result.Status).IsEqualTo(ResultStatus.Conflict);
            await Assert.That(ctx.Schools.Any(s => s.Id == school.Id)).IsTrue();
        }

        [Test]
        public async Task A_school_with_no_dependents_is_deleted()
        {
            using var ctx = TestDb.NewContext(nameof(A_school_with_no_dependents_is_deleted));
            var school = NewSchool();
            ctx.Schools.Add(school);
            await ctx.SaveChangesAsync();

            var result = await new DeleteSchoolCommandHandler(ctx).Handle(new DeleteSchoolCommand(school.Id), CancellationToken.None);

            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(ctx.Schools.Any(s => s.Id == school.Id)).IsFalse();
        }
    }
}
