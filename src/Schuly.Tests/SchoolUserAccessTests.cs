using Schuly.Domain;
using Schuly.Tests.TestHelpers;
using SUQ = Schuly.Application.Queries.SchoolUser;

namespace Schuly.Tests
{
    public class SchoolUserAccessTests
    {
        private static (Guid teacherUserId, Guid teacherSelf, Guid student, Guid outsider) Seed(string db)
        {
            var teacherUserId = Guid.NewGuid();
            var schoolId = Guid.NewGuid();
            var otherSchoolId = Guid.NewGuid();

            var student = TestDb.NewSchoolUser(schoolId, "Student");
            student.PrivateEmail = "student.private@example.com";
            student.PhoneNumber = "+41 33 333 33 33";
            student.Street = "Student Street 3";
            student.City = "Student City";
            student.Zip = "StudentZip3333";
            student.Grades.Add(new Grade { Score = 5, Weighting = 1, SchoolUserId = student.Id });
            student.Absences.Add(new Absence { Reason = "s", Type = default, From = default, Until = default, SchoolUserId = student.Id });

            var teacherSelf = TestDb.NewSchoolUser(schoolId, "Teacher");
            teacherSelf.ApplicationUserId = teacherUserId;
            teacherSelf.PrivateEmail = "teacher.private@example.com";
            teacherSelf.Grades.Add(new Grade { Score = 4, Weighting = 1, SchoolUserId = teacherSelf.Id });

            var outsider = TestDb.NewSchoolUser(otherSchoolId, "Outsider");

            var teacher = new Teacher
            {
                Id = Guid.NewGuid(),
                SchoolId = schoolId,
                FirstName = "T",
                LastName = "Eacher",
                Code = "TEA",
                ApplicationUserId = teacherUserId,
            };

            using var ctx = TestDb.NewContext(db);
            ctx.SchoolUsers.AddRange(student, teacherSelf, outsider);
            ctx.Teachers.Add(teacher);
            ctx.SaveChanges();
            return (teacherUserId, teacherSelf.Id, student.Id, outsider.Id);
        }

        [Test]
        public async Task Teacher_school_scope_comes_from_the_teachers_table()
        {
            var (teacherUserId, teacherSelf, student, outsider) = Seed(nameof(Teacher_school_scope_comes_from_the_teachers_table));
            using var ctx = TestDb.NewContext(nameof(Teacher_school_scope_comes_from_the_teachers_table));

            var handler = new SUQ.GetSchoolUsersQueryHandler(ctx, new FakeUserService(false) { IsTeacher = true, CurrentUserId = teacherUserId }, new FakeAvatarUrlSigner());
            var result = await handler.Handle(new SUQ.GetSchoolUsersQuery(), CancellationToken.None);

            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Value!.Count).IsEqualTo(2);
            await Assert.That(result.Value!.Any(su => su.Id == teacherSelf)).IsTrue();
            await Assert.That(result.Value!.Any(su => su.Id == student)).IsTrue();
            await Assert.That(result.Value!.Any(su => su.Id == outsider)).IsFalse();
        }

        [Test]
        public async Task Teacher_gets_no_pii_grades_or_absences_for_other_users()
        {
            var (teacherUserId, teacherSelf, student, _) = Seed(nameof(Teacher_gets_no_pii_grades_or_absences_for_other_users));
            using var ctx = TestDb.NewContext(nameof(Teacher_gets_no_pii_grades_or_absences_for_other_users));

            var handler = new SUQ.GetSchoolUsersQueryHandler(ctx, new FakeUserService(false) { IsTeacher = true, CurrentUserId = teacherUserId }, new FakeAvatarUrlSigner());
            var result = await handler.Handle(new SUQ.GetSchoolUsersQuery(), CancellationToken.None);

            var other = result.Value!.Single(su => su.Id == student);
            await Assert.That(other.PrivateEmail).IsNull();
            await Assert.That(other.PhoneNumber).IsNull();
            await Assert.That(other.Street).IsNull();
            await Assert.That(other.City).IsNull();
            await Assert.That(other.Zip).IsNull();
            await Assert.That(other.Birthday).IsNull();
            await Assert.That(other.ApplicationUserId).IsNull();
            await Assert.That(other.Grades.Count).IsEqualTo(0);
            await Assert.That(other.Absences.Count).IsEqualTo(0);

            var mine = result.Value!.Single(su => su.Id == teacherSelf);
            await Assert.That(mine.PrivateEmail).IsEqualTo("teacher.private@example.com");
            await Assert.That(mine.ApplicationUserId).IsEqualTo(teacherUserId);
            await Assert.That(mine.Grades.Count).IsEqualTo(1);
        }

        [Test]
        public async Task Administrator_gets_full_data_for_every_school_user()
        {
            var (_, _, student, _) = Seed(nameof(Administrator_gets_full_data_for_every_school_user));
            using var ctx = TestDb.NewContext(nameof(Administrator_gets_full_data_for_every_school_user));

            var handler = new SUQ.GetSchoolUsersQueryHandler(ctx, new FakeUserService(true), new FakeAvatarUrlSigner());
            var result = await handler.Handle(new SUQ.GetSchoolUsersQuery(), CancellationToken.None);

            await Assert.That(result.Value!.Count).IsEqualTo(3);
            var dto = result.Value!.Single(su => su.Id == student);
            await Assert.That(dto.PrivateEmail).IsEqualTo("student.private@example.com");
            await Assert.That(dto.Street).IsEqualTo("Student Street 3");
            await Assert.That(dto.Grades.Count).IsEqualTo(1);
            await Assert.That(dto.Absences.Count).IsEqualTo(1);
        }
    }
}
