using Schuly.Application.Commands.Absence;
using Schuly.Application.Queries.Absence;
using Schuly.Domain;
using Schuly.Domain.Enums;
using Schuly.Tests.TestHelpers;

namespace Schuly.Tests
{
    public class AbsenceAccessTests
    {
        private static (SchoolUser a, SchoolUser b) Seed(string dbName, out Guid otherAbsenceId)
        {
            var schoolId = Guid.NewGuid();
            var a = TestDb.NewSchoolUser(schoolId, "Alice");
            var b = TestDb.NewSchoolUser(schoolId, "Bob");

            var bAbsence = new Absence { Id = Guid.NewGuid(), Reason = "Sick", Type = default, From = default, Until = default, SchoolUserId = b.Id };
            otherAbsenceId = bAbsence.Id;

            using var ctx = TestDb.NewContext(dbName);
            ctx.SchoolUsers.AddRange(a, b);
            ctx.Absences.Add(new Absence { Id = Guid.NewGuid(), Reason = "Dentist", Type = default, From = default, Until = default, SchoolUserId = a.Id });
            ctx.Absences.Add(bAbsence);
            ctx.SaveChanges();
            return (a, b);
        }

        [Test]
        public async Task List_returns_only_the_callers_own_absences()
        {
            var (a, _) = Seed(nameof(List_returns_only_the_callers_own_absences), out _);
            using var ctx = TestDb.NewContext(nameof(List_returns_only_the_callers_own_absences));

            var handler = new GetAbsencesQueryHandler(ctx, new FakeUserService(false, a.Id));
            var result = await handler.Handle(new GetAbsencesQuery(), CancellationToken.None);

            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Value!.Count).IsEqualTo(1);
        }

        [Test]
        public async Task Admin_sees_every_absence()
        {
            Seed(nameof(Admin_sees_every_absence), out _);
            using var ctx = TestDb.NewContext(nameof(Admin_sees_every_absence));

            var handler = new GetAbsencesQueryHandler(ctx, new FakeUserService(true));
            var result = await handler.Handle(new GetAbsencesQuery(), CancellationToken.None);

            await Assert.That(result.Value!.Count).IsEqualTo(2);
        }

        [Test]
        public async Task Reading_another_users_absence_is_forbidden()
        {
            var (a, _) = Seed(nameof(Reading_another_users_absence_is_forbidden), out var otherId);
            using var ctx = TestDb.NewContext(nameof(Reading_another_users_absence_is_forbidden));

            var handler = new GetAbsenceQueryHandler(ctx, new FakeUserService(false, a.Id));
            var result = await handler.Handle(new GetAbsenceQuery(otherId), CancellationToken.None);

            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.Error).IsEqualTo("Forbidden");
        }

        [Test]
        public async Task Deleting_another_users_absence_is_forbidden_and_leaves_it_intact()
        {
            var (a, _) = Seed(nameof(Deleting_another_users_absence_is_forbidden_and_leaves_it_intact), out var otherId);
            using var ctx = TestDb.NewContext(nameof(Deleting_another_users_absence_is_forbidden_and_leaves_it_intact));

            var handler = new RemoveAbsenceCommandHandler(ctx, new FakeUserService(false, a.Id));
            var result = await handler.Handle(new RemoveAbsenceCommand(otherId), CancellationToken.None);

            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(ctx.Absences.Any(x => x.Id == otherId)).IsTrue();
        }

        [Test]
        public async Task Creating_an_absence_for_another_user_is_forbidden()
        {
            var (a, b) = Seed(nameof(Creating_an_absence_for_another_user_is_forbidden), out _);
            using var ctx = TestDb.NewContext(nameof(Creating_an_absence_for_another_user_is_forbidden));

            var handler = new CreateAbsenceCommandHandler(ctx, new FakeUserService(false, a.Id));
            var result = await handler.Handle(
                new CreateAbsenceCommand("Forged", default, default, default, b.Id), CancellationToken.None);

            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.Error).IsEqualTo("Forbidden");
        }
    }
}
