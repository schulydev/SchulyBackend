using Schuly.Application.Commands.Absence;
using Schuly.Application.Commands.Agenda;
using Schuly.Application.Commands.Me;
using Schuly.Application.Commands.StudentDocument;
using Schuly.Application.Models;
using Schuly.Application.Queries.Absence;
using Schuly.Application.Queries.Agenda;
using Schuly.Application.Queries.Class;
using Schuly.Application.Queries.Exam;
using Schuly.Application.Queries.School;
using Schuly.Application.Queries.SchoolUser;
using Schuly.Application.Queries.SemesterReport;
using Schuly.Application.Queries.StudentDocument;
using Schuly.Application.Queries.Teacher;
using Schuly.Domain;
using Schuly.Domain.Enums;
using Schuly.Infrastructure;
using Schuly.Tests.TestHelpers;

namespace Schuly.Tests
{
    /// <summary>
    /// Two users share a school and a class. There is no admin: every user, whoever they are,
    /// only ever sees and changes the rows of their own school users.
    /// </summary>
    public class PersonalDataScopeTests
    {
        private sealed record World(
            Guid UserA, Guid UserB, Guid SchoolUserA, Guid SchoolUserB, Guid SchoolId, Guid ClassId,
            Guid AbsenceA, Guid AbsenceB, Guid AgendaA, Guid AgendaB, Guid AgendaClassWide,
            Guid DocumentA, Guid DocumentB, Guid ReportA, Guid ReportB,
            Guid ExamId, Guid GradeA, Guid GradeB, Guid TeacherA, Guid TeacherB);

        private static World Seed(string db)
        {
            var schoolId = Guid.NewGuid();
            var a = TestDb.NewSchoolUser(schoolId, "Alice");
            var b = TestDb.NewSchoolUser(schoolId, "Bob");
            var cls = new Class { Name = "Shared", SchoolId = schoolId, Students = { a, b } };
            var exam = new Exam { Name = "E1", Type = default, Class = cls };
            var gradeA = new Grade { Id = Guid.NewGuid(), Score = 5, Weighting = 1, SchoolUserId = a.Id };
            var gradeB = new Grade { Id = Guid.NewGuid(), Score = 3, Weighting = 1, SchoolUserId = b.Id };
            exam.Grades.Add(gradeA);
            exam.Grades.Add(gradeB);

            var absenceA = new Absence { Id = Guid.NewGuid(), Reason = "a", Type = default, From = default, Until = default, SchoolUserId = a.Id };
            var absenceB = new Absence { Id = Guid.NewGuid(), Reason = "b", Type = default, From = default, Until = default, SchoolUserId = b.Id };
            var agendaA = new AgendaEntry { Id = Guid.NewGuid(), EntryType = default, Title = "mine", Date = DateTime.UtcNow, SchoolUserId = a.Id };
            var agendaB = new AgendaEntry { Id = Guid.NewGuid(), EntryType = default, Title = "theirs", Date = DateTime.UtcNow, SchoolUserId = b.Id };
            // A legacy class-wide row must not be a way to see the entries synced for another student.
            var agendaClassWide = new AgendaEntry { Id = Guid.NewGuid(), EntryType = default, Title = "class-wide", Date = DateTime.UtcNow, ClassId = cls.Id, SchoolUserId = b.Id };
            var docA = new StudentDocument { Id = Guid.NewGuid(), Title = "Zeugnis A", SchoolUserId = a.Id };
            var docB = new StudentDocument { Id = Guid.NewGuid(), Title = "Zeugnis B", SchoolUserId = b.Id };
            var reportA = new SemesterReport { Id = Guid.NewGuid(), ProgramCode = "P", SchoolYearStart = 2025, SemesterHalf = 1, ClassName = "Shared", SchoolUserId = a.Id };
            var reportB = new SemesterReport { Id = Guid.NewGuid(), ProgramCode = "P", SchoolYearStart = 2025, SemesterHalf = 1, ClassName = "Shared", SchoolUserId = b.Id };
            var teacherA = new Teacher { Id = Guid.NewGuid(), SchoolId = schoolId, FirstName = "T", LastName = "A", Code = "TA", ApplicationUserId = a.ApplicationUserId };
            var teacherB = new Teacher { Id = Guid.NewGuid(), SchoolId = schoolId, FirstName = "T", LastName = "B", Code = "TB", ApplicationUserId = b.ApplicationUserId };

            using var ctx = TestDb.NewContext(db);
            ctx.Schools.Add(new School { Id = schoolId, Name = "S" });
            ctx.SchoolUsers.AddRange(a, b);
            ctx.Classes.Add(cls);
            ctx.Exams.Add(exam);
            ctx.Absences.AddRange(absenceA, absenceB);
            ctx.AgendaEntries.AddRange(agendaA, agendaB, agendaClassWide);
            ctx.StudentDocuments.AddRange(docA, docB);
            ctx.SemesterReports.AddRange(reportA, reportB);
            ctx.Teachers.AddRange(teacherA, teacherB);
            ctx.SaveChanges();

            return new World(a.ApplicationUserId, b.ApplicationUserId, a.Id, b.Id, schoolId, cls.Id, absenceA.Id, absenceB.Id, agendaA.Id, agendaB.Id, agendaClassWide.Id, docA.Id, docB.Id, reportA.Id, reportB.Id, exam.Id, gradeA.Id, gradeB.Id, teacherA.Id, teacherB.Id);
        }

        private static FakeUserService AsA(World w) => new(w.SchoolUserA) { CurrentUserId = w.UserA };

        [Test]
        public async Task Absences_list_and_by_id_are_scoped_to_the_caller()
        {
            var w = Seed(nameof(Absences_list_and_by_id_are_scoped_to_the_caller));
            using var ctx = TestDb.NewContext(nameof(Absences_list_and_by_id_are_scoped_to_the_caller));

            var list = await new GetAbsencesQueryHandler(ctx, AsA(w)).Handle(new GetAbsencesQuery(), default);
            var theirs = await new GetAbsenceQueryHandler(ctx, AsA(w)).Handle(new GetAbsenceQuery(w.AbsenceB), default);
            var mine = await new GetAbsenceQueryHandler(ctx, AsA(w)).Handle(new GetAbsenceQuery(w.AbsenceA), default);

            await Assert.That(list.Value!.Select(x => x.Id)).IsEquivalentTo(new[] { w.AbsenceA });
            await Assert.That(theirs.Status).IsEqualTo(ResultStatus.NotFound);
            await Assert.That(mine.IsSuccess).IsTrue();
        }

        [Test]
        public async Task Absence_commands_on_another_users_row_are_not_found_and_change_nothing()
        {
            var w = Seed(nameof(Absence_commands_on_another_users_row_are_not_found_and_change_nothing));
            using var ctx = TestDb.NewContext(nameof(Absence_commands_on_another_users_row_are_not_found_and_change_nothing));

            var update = await new UpdateAbsenceCommandHandler(ctx, AsA(w)).Handle(new UpdateAbsenceCommand(w.AbsenceB, "hacked", default, DateTime.UnixEpoch, DateTime.UnixEpoch, w.SchoolUserA), default);
            var remove = await new RemoveAbsenceCommandHandler(ctx, AsA(w)).Handle(new RemoveAbsenceCommand(w.AbsenceB), default);

            await Assert.That(update.Status).IsEqualTo(ResultStatus.NotFound);
            await Assert.That(remove.Status).IsEqualTo(ResultStatus.NotFound);
            await Assert.That(ctx.Absences.Single(x => x.Id == w.AbsenceB).Reason).IsEqualTo("b");
            await Assert.That(ctx.Absences.Single(x => x.Id == w.AbsenceB).SchoolUserId).IsEqualTo(w.SchoolUserB);
        }

        [Test]
        public async Task Agenda_list_and_by_id_never_include_other_users_entries_even_when_class_scoped()
        {
            var w = Seed(nameof(Agenda_list_and_by_id_never_include_other_users_entries_even_when_class_scoped));
            using var ctx = TestDb.NewContext(nameof(Agenda_list_and_by_id_never_include_other_users_entries_even_when_class_scoped));

            var list = await new GetAgendasQueryHandler(ctx, AsA(w)).Handle(new GetAgendasQuery(), default);
            var theirs = await new GetAgendaQueryHandler(ctx, AsA(w)).Handle(new GetAgendaQuery(w.AgendaB), default);
            var classWide = await new GetAgendaQueryHandler(ctx, AsA(w)).Handle(new GetAgendaQuery(w.AgendaClassWide), default);

            await Assert.That(list.Value!.Select(x => x.Id)).IsEquivalentTo(new[] { w.AgendaA });
            await Assert.That(theirs.Status).IsEqualTo(ResultStatus.NotFound);
            await Assert.That(classWide.Status).IsEqualTo(ResultStatus.NotFound);
        }

        [Test]
        public async Task Agenda_commands_on_another_users_entry_are_not_found_and_change_nothing()
        {
            var w = Seed(nameof(Agenda_commands_on_another_users_entry_are_not_found_and_change_nothing));
            using var ctx = TestDb.NewContext(nameof(Agenda_commands_on_another_users_entry_are_not_found_and_change_nothing));

            var update = await new UpdateAgendaEntryCommandHandler(ctx, AsA(w)).Handle(new UpdateAgendaEntryCommand(w.AgendaB, default, "hacked", null, null, DateTime.UtcNow, null, null, w.SchoolUserA), default);
            var delete = await new DeleteAgendaEntryCommandHandler(ctx, AsA(w)).Handle(new DeleteAgendaEntryCommand(w.AgendaB), default);
            var create = await new CreateAgendaEntryCommandHandler(ctx, AsA(w)).Handle(new CreateAgendaEntryCommand(default, "forged", null, null, DateTime.UtcNow, null, null, w.SchoolUserB), default);

            await Assert.That(update.Status).IsEqualTo(ResultStatus.NotFound);
            await Assert.That(delete.Status).IsEqualTo(ResultStatus.NotFound);
            await Assert.That(create.Status).IsEqualTo(ResultStatus.Forbidden);
            await Assert.That(ctx.AgendaEntries.Single(x => x.Id == w.AgendaB).Title).IsEqualTo("theirs");
            await Assert.That(ctx.AgendaEntries.Any(x => x.Title == "forged")).IsFalse();
        }

        [Test]
        public async Task Documents_list_and_download_are_scoped_to_the_caller()
        {
            var w = Seed(nameof(Documents_list_and_download_are_scoped_to_the_caller));
            using var ctx = TestDb.NewContext(nameof(Documents_list_and_download_are_scoped_to_the_caller));

            var list = await new GetStudentDocumentsQueryHandler(ctx, AsA(w)).Handle(new GetStudentDocumentsQuery(), default);
            var download = await new OpenStudentDocumentQueryHandler(ctx, new FakeDocumentStorage(), AsA(w)).Handle(new OpenStudentDocumentQuery(w.DocumentB), default);

            await Assert.That(list.Value!.Select(x => x.Id)).IsEquivalentTo(new[] { w.DocumentA });
            await Assert.That(download.Status).IsEqualTo(ResultStatus.NotFound);
        }

        [Test]
        public async Task Uploading_a_document_for_another_users_profile_is_not_found()
        {
            var w = Seed(nameof(Uploading_a_document_for_another_users_profile_is_not_found));
            using var ctx = TestDb.NewContext(nameof(Uploading_a_document_for_another_users_profile_is_not_found));

            var result = await new UploadStudentDocumentCommandHandler(ctx, new InMemoryDocumentStorage(), AsA(w))
                .Handle(new UploadStudentDocumentCommand(w.SchoolUserB, new MemoryStream([1]), "f.pdf", "application/pdf", "t", null, null, null, null, null), default);

            await Assert.That(result.Status).IsEqualTo(ResultStatus.NotFound);
            await Assert.That(ctx.StudentDocuments.Count()).IsEqualTo(2);
        }

        [Test]
        public async Task Semester_reports_are_scoped_to_the_caller()
        {
            var w = Seed(nameof(Semester_reports_are_scoped_to_the_caller));
            using var ctx = TestDb.NewContext(nameof(Semester_reports_are_scoped_to_the_caller));

            var list = await new GetSemesterReportsQueryHandler(ctx, AsA(w)).Handle(new GetSemesterReportsQuery(), default);

            await Assert.That(list.Value!.Select(x => x.Id)).IsEquivalentTo(new[] { w.ReportA });
        }

        [Test]
        public async Task Exams_show_only_the_callers_own_grade_and_by_id_works_only_for_own_classes()
        {
            var w = Seed(nameof(Exams_show_only_the_callers_own_grade_and_by_id_works_only_for_own_classes));
            using var ctx = TestDb.NewContext(nameof(Exams_show_only_the_callers_own_grade_and_by_id_works_only_for_own_classes));
            var outsider = new FakeUserService(Guid.NewGuid()) { CurrentUserId = Guid.NewGuid() };

            var list = await new GetExamsQueryHandler(ctx, AsA(w)).Handle(new GetExamsQuery(), default);
            var mine = await new GetExamQueryHandler(ctx, AsA(w)).Handle(new GetExamQuery(w.ExamId), default);
            var asOutsider = await new GetExamQueryHandler(ctx, outsider).Handle(new GetExamQuery(w.ExamId), default);

            await Assert.That(list.Value!.Single().Grades.Select(g => g.Id)).IsEquivalentTo(new[] { w.GradeA });
            await Assert.That(mine.Value!.Grades.Select(g => g.Id)).IsEquivalentTo(new[] { w.GradeA });
            await Assert.That(asOutsider.Status).IsEqualTo(ResultStatus.NotFound);
        }

        [Test]
        public async Task Classes_show_neither_classmates_nor_their_grades_absences_or_agenda()
        {
            var w = Seed(nameof(Classes_show_neither_classmates_nor_their_grades_absences_or_agenda));
            using var ctx = TestDb.NewContext(nameof(Classes_show_neither_classmates_nor_their_grades_absences_or_agenda));
            var outsider = new FakeUserService(Guid.NewGuid()) { CurrentUserId = Guid.NewGuid() };

            var list = await new GetClassesQueryHandler(ctx, AsA(w), new FakeAvatarUrlSigner()).Handle(new GetClassesQuery(), default);
            var byId = await new GetClassQueryHandler(ctx, AsA(w), new FakeAvatarUrlSigner()).Handle(new GetClassQuery(w.ClassId), default);
            var asOutsider = await new GetClassQueryHandler(ctx, outsider, new FakeAvatarUrlSigner()).Handle(new GetClassQuery(w.ClassId), default);

            foreach (var cls in new[] { list.Value!.Single(), byId.Value! })
            {
                await Assert.That(cls.Students.Select(s => s.Id)).IsEquivalentTo(new[] { w.SchoolUserA });
                await Assert.That(cls.Students.Single().Absences.Select(x => x.Id)).IsEquivalentTo(new[] { w.AbsenceA });
                await Assert.That(cls.Students.Single().Grades.Select(x => x.Id)).IsEquivalentTo(new[] { w.GradeA });
                await Assert.That(cls.Exams.Single().Grades.Select(x => x.Id)).IsEquivalentTo(new[] { w.GradeA });
                await Assert.That(cls.Agenda.Any(x => x.Id == w.AgendaClassWide)).IsFalse();
            }
            await Assert.That(asOutsider.Status).IsEqualTo(ResultStatus.NotFound);
        }

        [Test]
        public async Task School_users_list_and_by_id_are_scoped_to_the_caller()
        {
            var w = Seed(nameof(School_users_list_and_by_id_are_scoped_to_the_caller));
            using var ctx = TestDb.NewContext(nameof(School_users_list_and_by_id_are_scoped_to_the_caller));

            var list = await new GetSchoolUsersQueryHandler(ctx, AsA(w), new FakeAvatarUrlSigner()).Handle(new GetSchoolUsersQuery(), default);
            var theirs = await new GetSchoolUserQueryHandler(ctx, AsA(w), new FakeAvatarUrlSigner()).Handle(new GetSchoolUserQuery(w.SchoolUserB), default);
            var mine = await new GetSchoolUserQueryHandler(ctx, AsA(w), new FakeAvatarUrlSigner()).Handle(new GetSchoolUserQuery(w.SchoolUserA), default);

            await Assert.That(list.Value!.Select(x => x.Id)).IsEquivalentTo(new[] { w.SchoolUserA });
            await Assert.That(theirs.Status).IsEqualTo(ResultStatus.NotFound);
            await Assert.That(mine.IsSuccess).IsTrue();
        }

        [Test]
        public async Task Teachers_and_schools_are_scoped_to_the_caller()
        {
            var w = Seed(nameof(Teachers_and_schools_are_scoped_to_the_caller));
            using var ctx = TestDb.NewContext(nameof(Teachers_and_schools_are_scoped_to_the_caller));
            var outsider = new FakeUserService(Guid.NewGuid()) { CurrentUserId = Guid.NewGuid() };

            var list = await new GetTeachersQueryHandler(ctx, AsA(w)).Handle(new GetTeachersQuery(), default);
            var theirs = await new GetTeacherQueryHandler(ctx, AsA(w)).Handle(new GetTeacherQuery(w.TeacherB), default);
            var ownSchool = await new GetSchoolQueryHandler(ctx, AsA(w)).Handle(new GetSchoolQuery(w.SchoolId), default);
            var foreignSchool = await new GetSchoolQueryHandler(ctx, outsider).Handle(new GetSchoolQuery(w.SchoolId), default);

            await Assert.That(list.Value!.Select(x => x.Id)).IsEquivalentTo(new[] { w.TeacherA });
            await Assert.That(theirs.Status).IsEqualTo(ResultStatus.NotFound);
            await Assert.That(ownSchool.IsSuccess).IsTrue();
            await Assert.That(foreignSchool.Status).IsEqualTo(ResultStatus.NotFound);
        }

        [Test]
        public async Task Grade_commands_on_another_users_grade_are_not_found_and_change_nothing()
        {
            var w = Seed(nameof(Grade_commands_on_another_users_grade_are_not_found_and_change_nothing));
            using var ctx = TestDb.NewContext(nameof(Grade_commands_on_another_users_grade_are_not_found_and_change_nothing));

            var update = await new UpdateMyGradeCommandHandler(ctx, AsA(w)).Handle(new UpdateMyGradeCommand(w.GradeB, 6, 1), default);
            var delete = await new DeleteMyGradeCommandHandler(ctx, AsA(w)).Handle(new DeleteMyGradeCommand(w.GradeB), default);

            await Assert.That(update.Status).IsEqualTo(ResultStatus.NotFound);
            await Assert.That(delete.Status).IsEqualTo(ResultStatus.NotFound);
            await Assert.That(ctx.Grades.Single(g => g.Id == w.GradeB).Score).IsEqualTo(3m);
        }
    }
}
