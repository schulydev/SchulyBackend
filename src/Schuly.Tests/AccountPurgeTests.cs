using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Schuly.Domain;
using Schuly.Domain.Enums;
using Schuly.Infrastructure.Services;
using Schuly.Tests.TestHelpers;

namespace Schuly.Tests
{
    public class AccountPurgeTests
    {
        private sealed record SeedResult(Guid User1Id, Guid User2Id, Guid SchoolUser1Id, Guid SchoolUser2Id, Guid ClassId, Guid TeacherId, Guid SubjectId1, string DocumentKey1, string DocumentKey2);

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

            var cls = new Class { Name = "C", SchoolId = schoolId, Students = { su1, su2 } };

            var exam = new Exam { Name = "E1", Type = default, Class = cls };
            exam.Grades.Add(new Grade { Score = 5, Weighting = 1, SchoolUserId = su1.Id });
            exam.Grades.Add(new Grade { Score = 4, Weighting = 1, SchoolUserId = su2.Id });

            su1.Absences.Add(new Absence { Reason = "a", Type = AbsenceType.Absence, From = DateTime.UtcNow, Until = DateTime.UtcNow, SchoolUserId = su1.Id });
            su2.Absences.Add(new Absence { Reason = "b", Type = AbsenceType.Absence, From = DateTime.UtcNow, Until = DateTime.UtcNow, SchoolUserId = su2.Id });

            var agenda1 = new AgendaEntry { EntryType = AgendaEntryType.Event, Title = "A1", Date = DateTime.UtcNow, SchoolUserId = su1.Id };
            var agenda2 = new AgendaEntry { EntryType = AgendaEntryType.Event, Title = "A2", Date = DateTime.UtcNow, SchoolUserId = su2.Id };

            var report1 = new SemesterReport { SchoolUserId = su1.Id, ProgramCode = "P", ClassName = "C", SchoolYearStart = 2024, SemesterHalf = 1 };
            var subject1 = new SemesterSubjectGrade { SubjectCode = "MATH", SubjectName = "Math" };
            report1.Subjects.Add(subject1);
            var report2 = new SemesterReport { SchoolUserId = su2.Id, ProgramCode = "P", ClassName = "C", SchoolYearStart = 2024, SemesterHalf = 1 };
            report2.Subjects.Add(new SemesterSubjectGrade { SubjectCode = "MATH", SubjectName = "Math" });

            var doc1 = new StudentDocument { SchoolUserId = su1.Id, Title = "Doc1", FileUrl = "key-1" };
            var doc2 = new StudentDocument { SchoolUserId = su2.Id, Title = "Doc2", FileUrl = "key-2" };

            var teacher = new Teacher { SchoolId = schoolId, ApplicationUserId = user1.Id, FirstName = "T", LastName = "X", Code = "TX" };

            using var ctx = TestDb.NewContext(db);
            ctx.Schools.Add(school);
            ctx.ApplicationUsers.AddRange(user1, user2);
            ctx.Classes.Add(cls);
            ctx.Exams.Add(exam);
            ctx.AgendaEntries.AddRange(agenda1, agenda2);
            ctx.SemesterReports.AddRange(report1, report2);
            ctx.StudentDocuments.AddRange(doc1, doc2);
            ctx.Teachers.Add(teacher);
            ctx.SaveChanges();

            return new SeedResult(user1.Id, user2.Id, su1.Id, su2.Id, cls.Id, teacher.Id, subject1.Id, doc1.FileUrl, doc2.FileUrl);
        }

        [Test]
        public async Task Purging_a_user_removes_all_their_owned_data()
        {
            var db = nameof(Purging_a_user_removes_all_their_owned_data);
            var seed = Seed(db);

            using var ctx = TestDb.NewContext(db);
            var storage = new FakeDocumentStorage();
            var purger = new AccountPurger(ctx, storage, NullLogger<AccountPurger>.Instance);

            await purger.PurgeApplicationUserAsync(seed.User1Id, CancellationToken.None);

            await Assert.That(ctx.ApplicationUsers.Any(u => u.Id == seed.User1Id)).IsFalse();
            await Assert.That(ctx.SchoolUsers.Any(su => su.Id == seed.SchoolUser1Id)).IsFalse();
            await Assert.That(ctx.Grades.Any(g => g.SchoolUserId == seed.SchoolUser1Id)).IsFalse();
            await Assert.That(ctx.Absences.Any(a => a.SchoolUserId == seed.SchoolUser1Id)).IsFalse();
            await Assert.That(ctx.AgendaEntries.Any(ae => ae.SchoolUserId == seed.SchoolUser1Id)).IsFalse();
            await Assert.That(ctx.SemesterReports.Any(r => r.SchoolUserId == seed.SchoolUser1Id)).IsFalse();
            await Assert.That(ctx.SemesterSubjectGrades.Any(sg => sg.Id == seed.SubjectId1)).IsFalse();
            await Assert.That(ctx.StudentDocuments.Any(d => d.SchoolUserId == seed.SchoolUser1Id)).IsFalse();

            var cls = await ctx.Classes.Include(c => c.Students).SingleAsync(c => c.Id == seed.ClassId);
            await Assert.That(cls.Students.Any(s => s.Id == seed.SchoolUser1Id)).IsFalse();
        }

        [Test]
        public async Task Purging_a_user_leaves_a_second_users_data_untouched()
        {
            var db = nameof(Purging_a_user_leaves_a_second_users_data_untouched);
            var seed = Seed(db);

            using var ctx = TestDb.NewContext(db);
            var storage = new FakeDocumentStorage();
            var purger = new AccountPurger(ctx, storage, NullLogger<AccountPurger>.Instance);

            await purger.PurgeApplicationUserAsync(seed.User1Id, CancellationToken.None);

            await Assert.That(ctx.ApplicationUsers.Any(u => u.Id == seed.User2Id)).IsTrue();
            await Assert.That(ctx.SchoolUsers.Any(su => su.Id == seed.SchoolUser2Id)).IsTrue();
            await Assert.That(ctx.Grades.Any(g => g.SchoolUserId == seed.SchoolUser2Id)).IsTrue();
            await Assert.That(ctx.Absences.Any(a => a.SchoolUserId == seed.SchoolUser2Id)).IsTrue();
            await Assert.That(ctx.AgendaEntries.Any(ae => ae.SchoolUserId == seed.SchoolUser2Id)).IsTrue();
            await Assert.That(ctx.SemesterReports.Any(r => r.SchoolUserId == seed.SchoolUser2Id)).IsTrue();
            await Assert.That(ctx.StudentDocuments.Any(d => d.SchoolUserId == seed.SchoolUser2Id)).IsTrue();
        }

        [Test]
        public async Task Purging_a_user_passes_the_documents_file_keys_to_storage_for_deletion()
        {
            var db = nameof(Purging_a_user_passes_the_documents_file_keys_to_storage_for_deletion);
            var seed = Seed(db);

            using var ctx = TestDb.NewContext(db);
            var storage = new FakeDocumentStorage();
            var purger = new AccountPurger(ctx, storage, NullLogger<AccountPurger>.Instance);

            await purger.PurgeApplicationUserAsync(seed.User1Id, CancellationToken.None);

            await Assert.That(storage.Deleted.Contains(seed.DocumentKey1)).IsTrue();
            await Assert.That(storage.Deleted.Contains(seed.DocumentKey2)).IsFalse();
        }

        [Test]
        public async Task Purging_still_removes_every_row_when_blob_deletion_fails()
        {
            var db = nameof(Purging_still_removes_every_row_when_blob_deletion_fails);
            var seed = Seed(db);

            using var ctx = TestDb.NewContext(db);
            var storage = new FakeDocumentStorage { ThrowOnDelete = true };
            var purger = new AccountPurger(ctx, storage, NullLogger<AccountPurger>.Instance);

            var summary = await purger.PurgeApplicationUserAsync(seed.User1Id, CancellationToken.None);

            await Assert.That(summary.BlobsFailed > 0).IsTrue();
            await Assert.That(ctx.ApplicationUsers.Any(u => u.Id == seed.User1Id)).IsFalse();
            await Assert.That(ctx.SchoolUsers.Any(su => su.Id == seed.SchoolUser1Id)).IsFalse();
            await Assert.That(ctx.StudentDocuments.Any(d => d.SchoolUserId == seed.SchoolUser1Id)).IsFalse();
        }

        [Test]
        public async Task Purging_a_user_unlinks_but_does_not_delete_their_teacher_record()
        {
            var db = nameof(Purging_a_user_unlinks_but_does_not_delete_their_teacher_record);
            var seed = Seed(db);

            using var ctx = TestDb.NewContext(db);
            var storage = new FakeDocumentStorage();
            var purger = new AccountPurger(ctx, storage, NullLogger<AccountPurger>.Instance);

            await purger.PurgeApplicationUserAsync(seed.User1Id, CancellationToken.None);

            var teacher = await ctx.Teachers.SingleOrDefaultAsync(t => t.Id == seed.TeacherId);
            await Assert.That(teacher).IsNotNull();
            await Assert.That(teacher!.ApplicationUserId).IsNull();
        }

        [Test]
        public async Task PurgeSchoolUsersAsync_removes_only_the_named_school_users_and_leaves_the_application_user_in_place()
        {
            var db = nameof(PurgeSchoolUsersAsync_removes_only_the_named_school_users_and_leaves_the_application_user_in_place);
            var seed = Seed(db);

            using var ctx = TestDb.NewContext(db);
            var storage = new FakeDocumentStorage();
            var purger = new AccountPurger(ctx, storage, NullLogger<AccountPurger>.Instance);

            await purger.PurgeSchoolUsersAsync([seed.SchoolUser1Id], CancellationToken.None);

            await Assert.That(ctx.ApplicationUsers.Any(u => u.Id == seed.User1Id)).IsTrue();
            await Assert.That(ctx.SchoolUsers.Any(su => su.Id == seed.SchoolUser1Id)).IsFalse();
            await Assert.That(ctx.Grades.Any(g => g.SchoolUserId == seed.SchoolUser1Id)).IsFalse();
            await Assert.That(ctx.StudentDocuments.Any(d => d.SchoolUserId == seed.SchoolUser1Id)).IsFalse();

            await Assert.That(ctx.SchoolUsers.Any(su => su.Id == seed.SchoolUser2Id)).IsTrue();
        }
    }
}
