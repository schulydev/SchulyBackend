using Schuly.Application.Queries.User;
using Schuly.Domain;
using Schuly.Domain.Enums;
using Schuly.Tests.TestHelpers;

namespace Schuly.Tests
{
    public class AccountExportTests
    {
        private sealed record SeedResult(Guid User1Id, Guid User2Id, Guid SchoolUser1Id, Guid SchoolUser2Id);

        private static SeedResult Seed(string db)
        {
            var schoolId = Guid.NewGuid();
            var school = new School { Id = schoolId, Name = "Test School" };

            var user1 = new ApplicationUser { Id = Guid.NewGuid(), ExternalId = "ext-1", Email = "u1@example.com" };
            var user2 = new ApplicationUser { Id = Guid.NewGuid(), ExternalId = "ext-2", Email = "u2@example.com" };

            var su1 = TestDb.NewSchoolUser(schoolId, "One");
            su1.ApplicationUserId = user1.Id;
            var su2 = TestDb.NewSchoolUser(schoolId, "Two");
            su2.ApplicationUserId = user2.Id;

            var exam = new Exam { Name = "E1", Type = default, Class = new Class { Name = "C", SchoolId = schoolId, Students = { su1, su2 } } };
            exam.Grades.Add(new Grade { Score = 5, Weighting = 1, SchoolUserId = su1.Id });
            exam.Grades.Add(new Grade { Score = 4, Weighting = 1, SchoolUserId = su2.Id });

            su1.Absences.Add(new Absence { Reason = "a", Type = AbsenceType.Absence, From = DateTime.UtcNow, Until = DateTime.UtcNow, SchoolUserId = su1.Id });
            su2.Absences.Add(new Absence { Reason = "b", Type = AbsenceType.Absence, From = DateTime.UtcNow, Until = DateTime.UtcNow, SchoolUserId = su2.Id });

            var agenda1 = new AgendaEntry { EntryType = AgendaEntryType.Event, Title = "A1", Date = DateTime.UtcNow, SchoolUserId = su1.Id };
            var agenda2 = new AgendaEntry { EntryType = AgendaEntryType.Event, Title = "A2", Date = DateTime.UtcNow, SchoolUserId = su2.Id };

            var report1 = new SemesterReport { SchoolUserId = su1.Id, ProgramCode = "P", ClassName = "C", SchoolYearStart = 2024, SemesterHalf = 1 };
            report1.Subjects.Add(new SemesterSubjectGrade { SubjectCode = "MATH", SubjectName = "Math" });
            var report2 = new SemesterReport { SchoolUserId = su2.Id, ProgramCode = "P", ClassName = "C", SchoolYearStart = 2024, SemesterHalf = 1 };
            report2.Subjects.Add(new SemesterSubjectGrade { SubjectCode = "MATH", SubjectName = "Math" });

            var doc1 = new StudentDocument { SchoolUserId = su1.Id, Title = "Doc1", FileUrl = "key-1" };
            var doc2 = new StudentDocument { SchoolUserId = su2.Id, Title = "Doc2", FileUrl = "key-2" };

            using var ctx = TestDb.NewContext(db);
            ctx.Schools.Add(school);
            ctx.ApplicationUsers.AddRange(user1, user2);
            ctx.Exams.Add(exam);
            ctx.AgendaEntries.AddRange(agenda1, agenda2);
            ctx.SemesterReports.AddRange(report1, report2);
            ctx.StudentDocuments.AddRange(doc1, doc2);
            ctx.SaveChanges();

            return new SeedResult(user1.Id, user2.Id, su1.Id, su2.Id);
        }

        [Test]
        public async Task Export_contains_only_the_callers_own_data()
        {
            var db = nameof(Export_contains_only_the_callers_own_data);
            var seed = Seed(db);

            using var ctx = TestDb.NewContext(db);
            var handler = new ExportCurrentUserQueryHandler(ctx, new FakeUserService(false) { CurrentUserId = seed.User1Id });

            var result = await handler.Handle(new ExportCurrentUserQuery(), CancellationToken.None);

            await Assert.That(result.IsSuccess).IsTrue();
            var export = result.Value!;

            await Assert.That(export.Profile.Id).IsEqualTo(seed.User1Id);

            await Assert.That(export.SchoolUsers.Count).IsEqualTo(1);
            await Assert.That(export.SchoolUsers.Single().Id).IsEqualTo(seed.SchoolUser1Id);
            await Assert.That(export.SchoolUsers.Single().Grades.Count).IsEqualTo(1);
            await Assert.That(export.SchoolUsers.Single().Absences.Count).IsEqualTo(1);

            await Assert.That(export.AgendaEntries.Count).IsEqualTo(1);
            await Assert.That(export.AgendaEntries.Single().SchoolUserId).IsEqualTo(seed.SchoolUser1Id);

            await Assert.That(export.SemesterReports.Count).IsEqualTo(1);
            await Assert.That(export.SemesterReports.Single().SchoolUserId).IsEqualTo(seed.SchoolUser1Id);
            await Assert.That(export.SemesterReports.Single().Subjects.Count).IsEqualTo(1);

            await Assert.That(export.Documents.Count).IsEqualTo(1);
            await Assert.That(export.Documents.Single().SchoolUserId).IsEqualTo(seed.SchoolUser1Id);

            await Assert.That(export.SchoolUsers.Any(su => su.Id == seed.SchoolUser2Id)).IsFalse();
            await Assert.That(export.AgendaEntries.Any(ae => ae.SchoolUserId == seed.SchoolUser2Id)).IsFalse();
            await Assert.That(export.SemesterReports.Any(r => r.SchoolUserId == seed.SchoolUser2Id)).IsFalse();
            await Assert.That(export.Documents.Any(d => d.SchoolUserId == seed.SchoolUser2Id)).IsFalse();
        }

        [Test]
        public async Task Export_fails_when_the_current_user_does_not_exist()
        {
            using var ctx = TestDb.NewContext(nameof(Export_fails_when_the_current_user_does_not_exist));
            var handler = new ExportCurrentUserQueryHandler(ctx, new FakeUserService(false) { CurrentUserId = Guid.NewGuid() });

            var result = await handler.Handle(new ExportCurrentUserQuery(), CancellationToken.None);

            await Assert.That(result.IsSuccess).IsFalse();
        }
    }
}
