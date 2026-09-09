using Schuly.Application.Commands.Absence;
using Schuly.Tests.TestHelpers;

namespace Schuly.Tests
{
    public class AbsenceValidationTests
    {
        [Test]
        public async Task Create_rejects_From_after_Until_for_a_user_allowed_to_write()
        {
            var schoolId = Guid.NewGuid();
            var alice = TestDb.NewSchoolUser(schoolId, "Alice");

            using var ctx = TestDb.NewContext(nameof(Create_rejects_From_after_Until_for_a_user_allowed_to_write));
            ctx.SchoolUsers.Add(alice);
            ctx.SaveChanges();

            var handler = new CreateAbsenceCommandHandler(ctx, new FakeUserService(false, alice.Id));
            var from = new DateTime(2026, 3, 5, 0, 0, 0, DateTimeKind.Utc);
            var until = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
            var result = await handler.Handle(new CreateAbsenceCommand("Sick", default, from, until, alice.Id), CancellationToken.None);

            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.Error).IsEqualTo("From must not be after Until");
            await Assert.That(ctx.Absences.Any()).IsFalse();
        }

        [Test]
        public async Task Update_rejects_From_after_Until_for_a_user_allowed_to_write()
        {
            var schoolId = Guid.NewGuid();
            var alice = TestDb.NewSchoolUser(schoolId, "Alice");
            var absence = new Domain.Absence { Id = Guid.NewGuid(), Reason = "Sick", Type = default, From = default, Until = default, SchoolUserId = alice.Id };

            using var ctx = TestDb.NewContext(nameof(Update_rejects_From_after_Until_for_a_user_allowed_to_write));
            ctx.SchoolUsers.Add(alice);
            ctx.Absences.Add(absence);
            ctx.SaveChanges();

            var handler = new UpdateAbsenceCommandHandler(ctx, new FakeUserService(false, alice.Id));
            var from = new DateTime(2026, 3, 5, 0, 0, 0, DateTimeKind.Utc);
            var until = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
            var result = await handler.Handle(new UpdateAbsenceCommand(absence.Id, "Sick", default, from, until, alice.Id), CancellationToken.None);

            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.Error).IsEqualTo("From must not be after Until");
        }
    }
}
