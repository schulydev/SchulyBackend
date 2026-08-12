using Schuly.Application.Queries.Exam;
using Schuly.Domain;
using Schuly.Domain.Enums;
using Schuly.Tests.TestHelpers;

namespace Schuly.Tests
{
    public class ExamAccessTests
    {
        private static Guid Seed(string dbName)
        {
            var schoolId = Guid.NewGuid();
            var alice = TestDb.NewSchoolUser(schoolId, "Alice");
            var carol = TestDb.NewSchoolUser(schoolId, "Carol");
            var bob = TestDb.NewSchoolUser(schoolId, "Bob");

            var class1 = new Class { Name = "Class 1", SchoolId = schoolId, Students = { alice, carol } };
            var class2 = new Class { Name = "Class 2", SchoolId = schoolId, Students = { bob } };

            var exam1 = new Exam { Name = "Exam 1", Type = default, Class = class1 };
            exam1.Grades.Add(new Grade { Score = 5.0m, Weighting = 1, SchoolUserId = alice.Id });
            exam1.Grades.Add(new Grade { Score = 4.0m, Weighting = 1, SchoolUserId = carol.Id });

            var exam2 = new Exam { Name = "Exam 2", Type = default, Class = class2 };
            exam2.Grades.Add(new Grade { Score = 3.0m, Weighting = 1, SchoolUserId = bob.Id });

            using var ctx = TestDb.NewContext(dbName);
            ctx.Classes.AddRange(class1, class2);
            ctx.Exams.AddRange(exam1, exam2);
            ctx.SaveChanges();
            return alice.Id;
        }

        [Test]
        public async Task Student_only_sees_exams_for_their_enrolled_classes()
        {
            var aliceId = Seed(nameof(Student_only_sees_exams_for_their_enrolled_classes));
            using var ctx = TestDb.NewContext(nameof(Student_only_sees_exams_for_their_enrolled_classes));

            var handler = new GetExamsQueryHandler(ctx, new FakeUserService(false, aliceId));
            var result = await handler.Handle(new GetExamsQuery(), CancellationToken.None);

            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Value!.Count).IsEqualTo(1);
        }

        [Test]
        public async Task Student_only_sees_their_own_grade_on_a_shared_exam()
        {
            var aliceId = Seed(nameof(Student_only_sees_their_own_grade_on_a_shared_exam));
            using var ctx = TestDb.NewContext(nameof(Student_only_sees_their_own_grade_on_a_shared_exam));

            var handler = new GetExamsQueryHandler(ctx, new FakeUserService(false, aliceId));
            var result = await handler.Handle(new GetExamsQuery(), CancellationToken.None);

            var exam = result.Value!.Single();
            await Assert.That(exam.Grades.Count).IsEqualTo(1);
        }

        [Test]
        public async Task Admin_sees_all_exams_and_all_grades()
        {
            Seed(nameof(Admin_sees_all_exams_and_all_grades));
            using var ctx = TestDb.NewContext(nameof(Admin_sees_all_exams_and_all_grades));

            var handler = new GetExamsQueryHandler(ctx, new FakeUserService(true));
            var result = await handler.Handle(new GetExamsQuery(), CancellationToken.None);

            await Assert.That(result.Value!.Count).IsEqualTo(2);
            await Assert.That(result.Value!.Sum(e => e.Grades.Count)).IsEqualTo(3);
        }
    }
}
