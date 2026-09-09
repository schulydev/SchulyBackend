using Schuly.Application.Queries.Exam;
using Schuly.Domain;
using Schuly.Tests.TestHelpers;

namespace Schuly.Tests
{
    public class ExamClassAverageTests
    {
        private static Guid Seed(string dbName)
        {
            var schoolId = Guid.NewGuid();
            var alice = TestDb.NewSchoolUser(schoolId, "Alice");
            var carol = TestDb.NewSchoolUser(schoolId, "Carol");

            var class1 = new Class { Name = "Class 1", SchoolId = schoolId, Students = { alice, carol } };
            var exam = new Exam { Name = "Exam 1", Type = default, Class = class1 };
            // Weighted average (6.0*2 + 3.0*1) / (2+1) = 5.0; unweighted mean would be 4.5.
            exam.Grades.Add(new Grade { Score = 6.0m, Weighting = 2, SchoolUserId = alice.Id });
            exam.Grades.Add(new Grade { Score = 3.0m, Weighting = 1, SchoolUserId = carol.Id });

            using var ctx = TestDb.NewContext(dbName);
            ctx.Classes.Add(class1);
            ctx.Exams.Add(exam);
            ctx.SaveChanges();
            return alice.Id;
        }

        [Test]
        public async Task Student_sees_the_weighted_class_average_over_all_grades_while_seeing_only_their_own_grade()
        {
            var aliceId = Seed(nameof(Student_sees_the_weighted_class_average_over_all_grades_while_seeing_only_their_own_grade));
            using var ctx = TestDb.NewContext(nameof(Student_sees_the_weighted_class_average_over_all_grades_while_seeing_only_their_own_grade));

            var handler = new GetExamsQueryHandler(ctx, new FakeUserService(false, aliceId));
            var result = await handler.Handle(new GetExamsQuery(), CancellationToken.None);

            await Assert.That(result.IsSuccess).IsTrue();
            var exam = result.Value!.Single();
            await Assert.That(exam.Grades.Count).IsEqualTo(1);
            await Assert.That(exam.ClassAverage).IsEqualTo(5.0m);
        }

        [Test]
        public async Task Admin_sees_the_same_weighted_class_average()
        {
            Seed(nameof(Admin_sees_the_same_weighted_class_average));
            using var ctx = TestDb.NewContext(nameof(Admin_sees_the_same_weighted_class_average));

            var handler = new GetExamsQueryHandler(ctx, new FakeUserService(true));
            var result = await handler.Handle(new GetExamsQuery(), CancellationToken.None);

            var exam = result.Value!.Single();
            await Assert.That(exam.ClassAverage).IsEqualTo(5.0m);
        }
    }
}
