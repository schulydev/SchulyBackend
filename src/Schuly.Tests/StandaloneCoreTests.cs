using Microsoft.EntityFrameworkCore;
using Schuly.Application.Commands.Absence;
using Schuly.Application.Commands.Me;
using Schuly.Application.Models;
using Schuly.Domain;
using Schuly.Domain.Enums;
using Schuly.Tests.TestHelpers;

namespace Schuly.Tests
{
    public class StandaloneCoreTests
    {
        private static async Task<Guid> MySchoolAsync(string db, Guid userId)
        {
            using var ctx = TestDb.NewContext(db);
            var result = await new CreateMySchoolCommandHandler(ctx, new FakeUserService(false) { CurrentUserId = userId })
                .Handle(new CreateMySchoolCommand("Home School", "Alex", "Muster"), CancellationToken.None);
            await Assert.That(result.IsSuccess).IsTrue();
            return result.Value;
        }

        [Test]
        public async Task A_user_can_create_their_own_school_as_its_student()
        {
            const string db = nameof(A_user_can_create_their_own_school_as_its_student);
            var userId = Guid.NewGuid();

            var studentId = await MySchoolAsync(db, userId);

            using var ctx = TestDb.NewContext(db);
            var student = await ctx.SchoolUsers.Include(su => su.School).SingleAsync(su => su.Id == studentId);
            await Assert.That(student.ApplicationUserId).IsEqualTo(userId);
            await Assert.That(student.Role).IsEqualTo(Roles.Student);
            await Assert.That(student.Source).IsNull();
            await Assert.That(student.School!.Name).IsEqualTo("Home School");
        }

        [Test]
        public async Task Adding_grades_creates_the_subject_and_exam_once_and_enrols_the_student()
        {
            const string db = nameof(Adding_grades_creates_the_subject_and_exam_once_and_enrols_the_student);
            var studentId = await MySchoolAsync(db, Guid.NewGuid());
            var me = new FakeUserService(false, studentId);

            using (var ctx = TestDb.NewContext(db))
                await new AddMyGradeCommandHandler(ctx, me).Handle(new AddMyGradeCommand(studentId, "Mathematik", "Test 1", 5.5m, 1, new DateOnly(2026, 10, 9)), CancellationToken.None);
            using (var ctx = TestDb.NewContext(db))
                await new AddMyGradeCommandHandler(ctx, me).Handle(new AddMyGradeCommand(studentId, "Mathematik", "Test 2", 4.5m, 2, null), CancellationToken.None);

            using var check = TestDb.NewContext(db);
            await Assert.That(await check.Classes.CountAsync()).IsEqualTo(1);
            await Assert.That(await check.Exams.CountAsync()).IsEqualTo(2);
            await Assert.That(await check.Grades.CountAsync(g => g.Source == null)).IsEqualTo(2);
            var student = await check.SchoolUsers.Include(su => su.Classes).SingleAsync(su => su.Id == studentId);
            await Assert.That(student.Classes.Count).IsEqualTo(1);
        }

        [Test]
        public async Task Adding_a_grade_for_someone_else_is_forbidden()
        {
            const string db = nameof(Adding_a_grade_for_someone_else_is_forbidden);
            var otherStudent = await MySchoolAsync(db, Guid.NewGuid());
            using var ctx = TestDb.NewContext(db);

            var result = await new AddMyGradeCommandHandler(ctx, new FakeUserService(false, Guid.NewGuid()))
                .Handle(new AddMyGradeCommand(otherStudent, "Mathematik", "Test 1", 5m, 1, null), CancellationToken.None);

            await Assert.That(result.Status).IsEqualTo(ResultStatus.Forbidden);
        }

        [Test]
        public async Task A_synced_grade_cannot_be_changed_or_deleted_by_hand()
        {
            const string db = nameof(A_synced_grade_cannot_be_changed_or_deleted_by_hand);
            var studentId = await MySchoolAsync(db, Guid.NewGuid());
            var gradeId = Guid.NewGuid();
            using (var ctx = TestDb.NewContext(db))
            {
                var schoolId = (await ctx.SchoolUsers.SingleAsync()).SchoolId;
                var exam = new Exam { Name = "Test 1", Class = new Class { Name = "Mathematik", SchoolId = schoolId } };
                ctx.Grades.Add(new Grade { Id = gradeId, Exam = exam, SchoolUserId = studentId, Score = 5, Weighting = 1, Source = "Schuly.Plugin.Schulware", ExternalId = "m-1" });
                await ctx.SaveChangesAsync();
            }
            var me = new FakeUserService(false, studentId);

            using var edit = TestDb.NewContext(db);
            var update = await new UpdateMyGradeCommandHandler(edit, me).Handle(new UpdateMyGradeCommand(gradeId, 6, 1), CancellationToken.None);
            var delete = await new DeleteMyGradeCommandHandler(edit, me).Handle(new DeleteMyGradeCommand(gradeId), CancellationToken.None);

            await Assert.That(update.Status).IsEqualTo(ResultStatus.Forbidden);
            await Assert.That(update.Error!).Contains("Schuly.Plugin.Schulware");
            await Assert.That(delete.Status).IsEqualTo(ResultStatus.Forbidden);
        }

        [Test]
        public async Task Deleting_the_last_grade_of_a_hand_made_exam_removes_the_exam()
        {
            const string db = nameof(Deleting_the_last_grade_of_a_hand_made_exam_removes_the_exam);
            var studentId = await MySchoolAsync(db, Guid.NewGuid());
            var me = new FakeUserService(false, studentId);
            Guid gradeId;
            using (var ctx = TestDb.NewContext(db))
                gradeId = (await new AddMyGradeCommandHandler(ctx, me).Handle(new AddMyGradeCommand(studentId, "Englisch", "Vocab", 5m, 1, null), CancellationToken.None)).Value;

            using (var ctx = TestDb.NewContext(db))
                await new DeleteMyGradeCommandHandler(ctx, me).Handle(new DeleteMyGradeCommand(gradeId), CancellationToken.None);

            using var check = TestDb.NewContext(db);
            await Assert.That(await check.Grades.CountAsync()).IsEqualTo(0);
            await Assert.That(await check.Exams.CountAsync()).IsEqualTo(0);
        }

        [Test]
        public async Task An_absence_time_without_an_offset_is_rejected()
        {
            const string db = nameof(An_absence_time_without_an_offset_is_rejected);
            var studentId = await MySchoolAsync(db, Guid.NewGuid());
            using var ctx = TestDb.NewContext(db);
            var local = new DateTime(2026, 10, 23, 8, 0, 0, DateTimeKind.Unspecified);

            var result = await new CreateAbsenceCommandHandler(ctx, new FakeUserService(false, studentId))
                .Handle(new CreateAbsenceCommand("Sick", AbsenceType.Absence, local, local, studentId), CancellationToken.None);

            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.Error!).Contains("UTC offset");
        }

        [Test]
        public async Task A_synced_absence_cannot_be_removed_by_hand()
        {
            const string db = nameof(A_synced_absence_cannot_be_removed_by_hand);
            var studentId = await MySchoolAsync(db, Guid.NewGuid());
            var absence = new Absence { SchoolUserId = studentId, Reason = "Sick", Type = AbsenceType.Absence, From = DateTime.UtcNow, Until = DateTime.UtcNow, Source = "Schuly.Plugin.Schulware", ExternalId = "a" };
            using (var ctx = TestDb.NewContext(db))
            {
                ctx.Absences.Add(absence);
                await ctx.SaveChangesAsync();
            }

            using var edit = TestDb.NewContext(db);
            var result = await new RemoveAbsenceCommandHandler(edit, new FakeUserService(false, studentId)).Handle(new RemoveAbsenceCommand(absence.Id), CancellationToken.None);

            await Assert.That(result.Status).IsEqualTo(ResultStatus.Forbidden);
        }
    }
}
