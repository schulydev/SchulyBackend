using Microsoft.EntityFrameworkCore;
using Schuly.Domain;
using Schuly.Domain.Enums;
using Schuly.Infrastructure.Plugins;
using Schuly.Plugin.Abstractions;
using Schuly.Tests.TestHelpers;

namespace Schuly.Tests
{
    public class PluginSchoolDataTests
    {
        private const string Source = "Schuly.Plugin.Test";

        private static async Task<(PluginSchoolData Data, Guid SchoolUserId)> ProvisionedAsync(string dbName, string source = Source)
        {
            var data = new PluginSchoolData(TestDb.NewContext(dbName), source);
            var schoolId = await data.EnsureSchoolAsync(new SchoolRecord("Test School", "https://school.example"));
            var schoolUserId = await data.EnsureStudentAsync(schoolId, Guid.NewGuid(), new StudentRecord { FirstName = "Alex", Email = "alex@school.example" });
            return (data, schoolUserId);
        }

        [Test]
        public async Task EnsureSchool_finds_an_existing_school_by_website()
        {
            var data = new PluginSchoolData(TestDb.NewContext(nameof(EnsureSchool_finds_an_existing_school_by_website)), Source);

            var first = await data.EnsureSchoolAsync(new SchoolRecord("Test School", "https://school.example"));
            var second = await data.EnsureSchoolAsync(new SchoolRecord("Renamed", "https://school.example", "https://school.example/logo.png"));

            await Assert.That(second).IsEqualTo(first);
            using var ctx = TestDb.NewContext(nameof(EnsureSchool_finds_an_existing_school_by_website));
            await Assert.That(await ctx.Schools.CountAsync()).IsEqualTo(1);
            await Assert.That((await ctx.Schools.SingleAsync()).LogoUrl).IsEqualTo("https://school.example/logo.png");
        }

        [Test]
        public async Task EnsureStudent_adopts_a_student_without_source_and_rejects_another_plugin()
        {
            const string db = nameof(EnsureStudent_adopts_a_student_without_source_and_rejects_another_plugin);
            var schoolId = Guid.NewGuid();
            var existing = TestDb.NewSchoolUser(schoolId);
            using (var ctx = TestDb.NewContext(db))
            {
                ctx.SchoolUsers.Add(existing);
                await ctx.SaveChangesAsync();
            }

            var adopted = await new PluginSchoolData(TestDb.NewContext(db), Source).EnsureStudentAsync(schoolId, existing.ApplicationUserId, new StudentRecord());
            await Assert.That(adopted).IsEqualTo(existing.Id);

            var other = new PluginSchoolData(TestDb.NewContext(db), "Schuly.Plugin.Other");
            await Assert.That(() => other.EnsureStudentAsync(schoolId, existing.ApplicationUserId, new StudentRecord())).Throws<UnauthorizedAccessException>();
        }

        [Test]
        public async Task SyncAgenda_upserts_by_external_id_and_stores_utc()
        {
            const string db = nameof(SyncAgenda_upserts_by_external_id_and_stores_utc);
            var (data, studentId) = await ProvisionedAsync(db);
            var swiss = TimeSpan.FromHours(2);

            var first = await data.SyncAgendaAsync(studentId, [
                new AgendaRecord("lesson-1", AgendaKind.Lesson, "Mathematik", new DateTimeOffset(2026, 10, 23, 8, 0, 0, swiss), new DateTimeOffset(2026, 10, 23, 9, 30, 0, swiss), "2.14"),
                new AgendaRecord("lesson-2", AgendaKind.Lesson, "Sport", new DateTimeOffset(2026, 10, 23, 9, 45, 0, swiss)),
            ]);
            var second = await data.SyncAgendaAsync(studentId, [
                new AgendaRecord("lesson-1", AgendaKind.Lesson, "Mathematik", new DateTimeOffset(2026, 10, 23, 8, 0, 0, swiss), new DateTimeOffset(2026, 10, 23, 9, 30, 0, swiss), "1.05"),
                new AgendaRecord("lesson-2", AgendaKind.Lesson, "Sport", new DateTimeOffset(2026, 10, 23, 9, 45, 0, swiss)),
            ]);

            await Assert.That(first).IsEqualTo(new SyncResult(2, 0));
            await Assert.That(second).IsEqualTo(new SyncResult(0, 1));

            using var ctx = TestDb.NewContext(db);
            var maths = await ctx.AgendaEntries.SingleAsync(a => a.ExternalId == "lesson-1");
            await Assert.That(maths.Date).IsEqualTo(new DateTime(2026, 10, 23, 6, 0, 0, DateTimeKind.Utc));
            await Assert.That(maths.Place).IsEqualTo("1.05");
            await Assert.That(maths.Source).IsEqualTo(Source);
            await Assert.That(maths.EntryType).IsEqualTo(AgendaEntryType.Lesson);
        }

        [Test]
        public async Task Sync_refuses_a_student_the_plugin_did_not_provision()
        {
            const string db = nameof(Sync_refuses_a_student_the_plugin_did_not_provision);
            var (_, studentId) = await ProvisionedAsync(db);
            var other = new PluginSchoolData(TestDb.NewContext(db), "Schuly.Plugin.Other");

            await Assert.That(async () => { await other.SyncAbsencesAsync(studentId, [new AbsenceRecord("a", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)]); }).Throws<UnauthorizedAccessException>();
        }

        [Test]
        public async Task Sync_adopts_a_student_provisioned_before_sources_existed()
        {
            const string db = nameof(Sync_adopts_a_student_provisioned_before_sources_existed);
            var student = TestDb.NewSchoolUser(Guid.NewGuid());
            using (var ctx = TestDb.NewContext(db))
            {
                ctx.SchoolUsers.Add(student);
                await ctx.SaveChangesAsync();
            }

            var result = await new PluginSchoolData(TestDb.NewContext(db), Source).SyncAbsencesAsync(student.Id, [new AbsenceRecord("a", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)]);

            await Assert.That(result).IsEqualTo(new SyncResult(1, 0));
            using var check = TestDb.NewContext(db);
            await Assert.That((await check.SchoolUsers.SingleAsync()).Source).IsEqualTo(Source);
        }

        [Test]
        public async Task Sync_leaves_manual_rows_alone()
        {
            const string db = nameof(Sync_leaves_manual_rows_alone);
            var (data, studentId) = await ProvisionedAsync(db);
            using (var ctx = TestDb.NewContext(db))
            {
                ctx.Absences.Add(new Absence { SchoolUserId = studentId, Reason = "Doctor", Type = AbsenceType.Absence, From = DateTime.UtcNow, Until = DateTime.UtcNow, ExternalId = "a" });
                await ctx.SaveChangesAsync();
            }

            var result = await data.SyncAbsencesAsync(studentId, [new AbsenceRecord("a", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "Sick")]);

            await Assert.That(result).IsEqualTo(new SyncResult(1, 0));
            using var check = TestDb.NewContext(db);
            await Assert.That(await check.Absences.CountAsync(a => a.Source == null && a.Reason == "Doctor")).IsEqualTo(1);
        }

        [Test]
        public async Task SyncGrades_creates_subject_and_exam_once_and_enrols_the_student()
        {
            const string db = nameof(SyncGrades_creates_subject_and_exam_once_and_enrols_the_student);
            var (data, studentId) = await ProvisionedAsync(db);

            var first = await data.SyncGradesAsync(studentId, [
                new GradeRecord("m-1", "Mathematik", "Test 1", 5.5m, 1, new DateOnly(2026, 10, 9)),
                new GradeRecord("m-2", "Mathematik", "Test 2", 4.0m, 2),
            ]);
            var second = await data.SyncGradesAsync(studentId, [new GradeRecord("m-1", "Mathematik", "Test 1", 6.0m)]);

            await Assert.That(first).IsEqualTo(new SyncResult(2, 0));
            await Assert.That(second).IsEqualTo(new SyncResult(0, 1));

            using var ctx = TestDb.NewContext(db);
            await Assert.That(await ctx.Classes.CountAsync()).IsEqualTo(1);
            await Assert.That(await ctx.Exams.CountAsync()).IsEqualTo(2);
            var student = await ctx.SchoolUsers.Include(su => su.Classes).SingleAsync(su => su.Id == studentId);
            await Assert.That(student.Classes.Single().Name).IsEqualTo("Mathematik");
            await Assert.That((await ctx.Grades.SingleAsync(g => g.ExternalId == "m-1")).Score).IsEqualTo(6.0m);
        }

        [Test]
        public async Task SyncDocuments_keeps_the_stored_file_when_a_resync_has_none()
        {
            const string db = nameof(SyncDocuments_keeps_the_stored_file_when_a_resync_has_none);
            var (data, studentId) = await ProvisionedAsync(db);

            await data.SyncDocumentsAsync(studentId, [new DocumentRecord("zeugnis-1", "Zeugnis HS25", "zeugnis.pdf", File: new StoredFile("blob-1", 1234))]);
            await data.SyncDocumentsAsync(studentId, [new DocumentRecord("zeugnis-1", "Zeugnis HS25", "zeugnis.pdf")]);

            await Assert.That(await data.GetDocumentIdsAsync(studentId)).Contains("zeugnis-1");
            using var ctx = TestDb.NewContext(db);
            await Assert.That((await ctx.StudentDocuments.SingleAsync()).FileUrl).IsEqualTo("blob-1");
        }

        [Test]
        public async Task Sync_rejects_a_blank_external_id()
        {
            var (data, studentId) = await ProvisionedAsync(nameof(Sync_rejects_a_blank_external_id));

            await Assert.That(async () => { await data.SyncAgendaAsync(studentId, [new AgendaRecord(" ", AgendaKind.Event, "x", DateTimeOffset.UtcNow)]); }).Throws<ArgumentException>();
        }

        [Test]
        public async Task An_unchanged_resync_produces_no_notifications()
        {
            const string db = nameof(An_unchanged_resync_produces_no_notifications);
            var (data, studentId) = await ProvisionedAsync(db);
            var lesson = new AgendaRecord("lesson-1", AgendaKind.Lesson, "Mathematik", new DateTimeOffset(2026, 10, 23, 8, 0, 0, TimeSpan.FromHours(2)));
            await data.SyncAgendaAsync(studentId, [lesson]);

            var notifying = new PluginSchoolData(TestDb.NewNotifyingContext(db), Source);
            var result = await notifying.SyncAgendaAsync(studentId, [lesson]);

            await Assert.That(result).IsEqualTo(new SyncResult(0, 0));
            using var ctx = TestDb.NewContext(db);
            await Assert.That(await ctx.NotificationOutbox.CountAsync()).IsEqualTo(0);
        }
    }
}
