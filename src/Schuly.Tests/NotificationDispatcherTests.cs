using Microsoft.Extensions.Logging.Abstractions;
using Schuly.API.Services.Notifications;
using Schuly.Domain;
using Schuly.Domain.Enums;
using Schuly.Tests.TestHelpers;

namespace Schuly.Tests
{
    public class NotificationDispatcherTests
    {
        private static NotificationOutbox NewRow(Guid userId, NotificationType type, DateTime now, string? subjectName = null, string? summary = null, int retryCount = 0) => new()
        {
            Id = Guid.NewGuid(),
            ApplicationUserId = userId,
            Type = type,
            EntityId = Guid.NewGuid(),
            SubjectName = subjectName,
            Summary = summary,
            Status = NotificationOutboxStatus.Pending,
            RetryCount = retryCount,
            NextAttemptAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        };

        [Test]
        public async Task Row_whose_category_is_disabled_is_marked_sent_without_sending()
        {
            var dbName = nameof(Row_whose_category_is_disabled_is_marked_sent_without_sending);
            using var ctx = TestDb.NewContext(dbName);
            var now = DateTime.UtcNow;
            var userId = Guid.NewGuid();

            ctx.NotificationPreferences.Add(new NotificationPreference { ApplicationUserId = userId, Grades = false, Absences = true, Agenda = true });
            var row = NewRow(userId, NotificationType.GradeAdded, now, subjectName: "Mathematik");
            ctx.NotificationOutbox.Add(row);
            ctx.SaveChanges();

            var sender = new FakePushSender();
            var dispatcher = new NotificationDispatcher(ctx, sender, NullLogger<NotificationDispatcher>.Instance);

            var processed = await dispatcher.DrainOnceAsync(CancellationToken.None);

            await Assert.That(processed).IsEqualTo(1);
            await Assert.That(row.Status).IsEqualTo(NotificationOutboxStatus.Sent);
            await Assert.That(sender.Sends.Count).IsEqualTo(0);
        }

        [Test]
        public async Task User_with_no_device_tokens_has_rows_marked_sent()
        {
            var dbName = nameof(User_with_no_device_tokens_has_rows_marked_sent);
            using var ctx = TestDb.NewContext(dbName);
            var now = DateTime.UtcNow;
            var userId = Guid.NewGuid();

            var row = NewRow(userId, NotificationType.AbsenceAdded, now, summary: "Krank");
            ctx.NotificationOutbox.Add(row);
            ctx.SaveChanges();

            var sender = new FakePushSender();
            var dispatcher = new NotificationDispatcher(ctx, sender, NullLogger<NotificationDispatcher>.Instance);

            var processed = await dispatcher.DrainOnceAsync(CancellationToken.None);

            await Assert.That(processed).IsEqualTo(1);
            await Assert.That(row.Status).IsEqualTo(NotificationOutboxStatus.Sent);
            await Assert.That(sender.Sends.Count).IsEqualTo(0);
        }

        [Test]
        public async Task De_and_en_tokens_each_get_their_own_send()
        {
            var dbName = nameof(De_and_en_tokens_each_get_their_own_send);
            using var ctx = TestDb.NewContext(dbName);
            var now = DateTime.UtcNow;
            var userId = Guid.NewGuid();

            ctx.DeviceTokens.Add(new DeviceToken { ApplicationUserId = userId, Token = "de-token", Platform = "android", Locale = "de", LastSeenAt = now });
            ctx.DeviceTokens.Add(new DeviceToken { ApplicationUserId = userId, Token = "en-token", Platform = "ios", Locale = "en", LastSeenAt = now });
            var row = NewRow(userId, NotificationType.AbsenceAdded, now, summary: "Krank");
            ctx.NotificationOutbox.Add(row);
            ctx.SaveChanges();

            var sender = new FakePushSender();
            var dispatcher = new NotificationDispatcher(ctx, sender, NullLogger<NotificationDispatcher>.Instance);

            var processed = await dispatcher.DrainOnceAsync(CancellationToken.None);

            await Assert.That(processed).IsEqualTo(1);
            await Assert.That(row.Status).IsEqualTo(NotificationOutboxStatus.Sent);
            await Assert.That(sender.Sends.Count).IsEqualTo(2);

            var deSend = sender.Sends.Single(s => s.Tokens.Contains("de-token"));
            var enSend = sender.Sends.Single(s => s.Tokens.Contains("en-token"));
            await Assert.That(deSend.Message.Title).IsEqualTo("Neue Absenz");
            await Assert.That(enSend.Message.Title).IsEqualTo("New absence");
        }

        [Test]
        public async Task Three_grade_added_rows_for_one_user_produce_a_single_aggregated_send()
        {
            var dbName = nameof(Three_grade_added_rows_for_one_user_produce_a_single_aggregated_send);
            using var ctx = TestDb.NewContext(dbName);
            var now = DateTime.UtcNow;
            var userId = Guid.NewGuid();

            ctx.DeviceTokens.Add(new DeviceToken { ApplicationUserId = userId, Token = "token-1", Platform = "android", Locale = "de", LastSeenAt = now });
            ctx.NotificationOutbox.Add(NewRow(userId, NotificationType.GradeAdded, now, subjectName: "Mathematik"));
            ctx.NotificationOutbox.Add(NewRow(userId, NotificationType.GradeAdded, now, subjectName: "Deutsch"));
            ctx.NotificationOutbox.Add(NewRow(userId, NotificationType.GradeAdded, now, subjectName: "Englisch"));
            ctx.SaveChanges();

            var sender = new FakePushSender();
            var dispatcher = new NotificationDispatcher(ctx, sender, NullLogger<NotificationDispatcher>.Instance);

            var processed = await dispatcher.DrainOnceAsync(CancellationToken.None);

            await Assert.That(processed).IsEqualTo(3);
            await Assert.That(sender.Sends.Count).IsEqualTo(1);
            await Assert.That(sender.Sends[0].Message.Title).IsEqualTo("3 neue Noten");
            await Assert.That(ctx.NotificationOutbox.Count(r => r.Status == NotificationOutboxStatus.Sent)).IsEqualTo(3);
        }

        [Test]
        public async Task Unregistered_token_is_deleted_from_device_tokens()
        {
            var dbName = nameof(Unregistered_token_is_deleted_from_device_tokens);
            using var ctx = TestDb.NewContext(dbName);
            var now = DateTime.UtcNow;
            var userId = Guid.NewGuid();

            ctx.DeviceTokens.Add(new DeviceToken { ApplicationUserId = userId, Token = "dead-token", Platform = "android", Locale = "de", LastSeenAt = now });
            ctx.NotificationOutbox.Add(NewRow(userId, NotificationType.AbsenceAdded, now, summary: "Krank"));
            ctx.SaveChanges();

            var sender = new FakePushSender { UnregisteredTokensToReturn = ["dead-token"] };
            var dispatcher = new NotificationDispatcher(ctx, sender, NullLogger<NotificationDispatcher>.Instance);

            await dispatcher.DrainOnceAsync(CancellationToken.None);

            await Assert.That(ctx.DeviceTokens.Any(t => t.Token == "dead-token")).IsFalse();
        }

        [Test]
        public async Task Throwing_sender_leaves_the_row_pending_with_retry_count_incremented()
        {
            var dbName = nameof(Throwing_sender_leaves_the_row_pending_with_retry_count_incremented);
            using var ctx = TestDb.NewContext(dbName);
            var now = DateTime.UtcNow;
            var userId = Guid.NewGuid();

            ctx.DeviceTokens.Add(new DeviceToken { ApplicationUserId = userId, Token = "token-1", Platform = "android", Locale = "de", LastSeenAt = now });
            var row = NewRow(userId, NotificationType.AbsenceAdded, now, summary: "Krank");
            ctx.NotificationOutbox.Add(row);
            ctx.SaveChanges();

            var sender = new FakePushSender { ExceptionToThrow = new InvalidOperationException("boom") };
            var dispatcher = new NotificationDispatcher(ctx, sender, NullLogger<NotificationDispatcher>.Instance);

            await dispatcher.DrainOnceAsync(CancellationToken.None);

            await Assert.That(row.Status).IsEqualTo(NotificationOutboxStatus.Pending);
            await Assert.That(row.RetryCount).IsEqualTo(1);
            await Assert.That(row.NextAttemptAt).IsGreaterThan(now);
            await Assert.That(row.LastError).IsEqualTo("boom");
        }

        [Test]
        public async Task Row_at_retry_count_four_that_fails_again_becomes_failed()
        {
            var dbName = nameof(Row_at_retry_count_four_that_fails_again_becomes_failed);
            using var ctx = TestDb.NewContext(dbName);
            var now = DateTime.UtcNow;
            var userId = Guid.NewGuid();

            ctx.DeviceTokens.Add(new DeviceToken { ApplicationUserId = userId, Token = "token-1", Platform = "android", Locale = "de", LastSeenAt = now });
            var row = NewRow(userId, NotificationType.AbsenceAdded, now, summary: "Krank", retryCount: 4);
            ctx.NotificationOutbox.Add(row);
            ctx.SaveChanges();

            var sender = new FakePushSender { ExceptionToThrow = new InvalidOperationException("boom") };
            var dispatcher = new NotificationDispatcher(ctx, sender, NullLogger<NotificationDispatcher>.Instance);

            await dispatcher.DrainOnceAsync(CancellationToken.None);

            await Assert.That(row.Status).IsEqualTo(NotificationOutboxStatus.Failed);
            await Assert.That(row.RetryCount).IsEqualTo(5);
        }

        [Test]
        public async Task Unconfigured_sender_drains_the_batch_by_marking_rows_sent()
        {
            var dbName = nameof(Unconfigured_sender_drains_the_batch_by_marking_rows_sent);
            using var ctx = TestDb.NewContext(dbName);
            var now = DateTime.UtcNow;
            var userId = Guid.NewGuid();

            var row = NewRow(userId, NotificationType.AbsenceAdded, now, summary: "Krank");
            ctx.NotificationOutbox.Add(row);
            ctx.SaveChanges();

            var sender = new FakePushSender { IsConfigured = false };
            var dispatcher = new NotificationDispatcher(ctx, sender, NullLogger<NotificationDispatcher>.Instance);

            var processed = await dispatcher.DrainOnceAsync(CancellationToken.None);

            await Assert.That(processed).IsEqualTo(1);
            await Assert.That(row.Status).IsEqualTo(NotificationOutboxStatus.Sent);
            await Assert.That(sender.Sends.Count).IsEqualTo(0);
        }
    }
}
