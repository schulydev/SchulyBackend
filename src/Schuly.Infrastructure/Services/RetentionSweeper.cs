using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Schuly.Infrastructure.Services
{
    public record RetentionSweepResult(int SchoolUsersPurged, int AccountsPurged);

    public sealed class RetentionSweeper(SchulyDbContext dbContext, IAccountPurger accountPurger, IOptions<RetentionOptions> options, ILogger<RetentionSweeper> logger)
    {
        public async Task<RetentionSweepResult> SweepAsync(CancellationToken cancellationToken)
        {
            if (!options.Value.Enabled)
            {
                logger.LogDebug("Retention sweep is disabled; skipping.");
                return new RetentionSweepResult(0, 0);
            }

            var schoolUsersPurged = await SweepLeaversAsync(cancellationToken);
            var accountsPurged = await SweepInactiveAsync(cancellationToken);

            if (schoolUsersPurged > 0 || accountsPurged > 0)
                logger.LogInformation("Retention sweep purged {SchoolUsersPurged} school users and {AccountsPurged} accounts.", schoolUsersPurged, accountsPurged);

            return new RetentionSweepResult(schoolUsersPurged, accountsPurged);
        }

        private async Task<int> SweepLeaversAsync(CancellationToken cancellationToken)
        {
            var leaveCutoff = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(-options.Value.MonthsAfterLeave);

            var schoolUserIds = await dbContext.SchoolUsers
                .Where(su => su.LeaveDate != null && su.LeaveDate < leaveCutoff)
                .Select(su => su.Id)
                .ToListAsync(cancellationToken);

            if (schoolUserIds.Count == 0)
                return 0;

            var summary = await accountPurger.PurgeSchoolUsersAsync(schoolUserIds, cancellationToken);
            return summary.SchoolUsers;
        }

        private async Task<int> SweepInactiveAsync(CancellationToken cancellationToken)
        {
            var inactiveCutoff = DateTime.UtcNow.AddMonths(-options.Value.MonthsInactive);

            // Falling back to CreatedAt covers accounts created before LastSeenAt
            // existed, and identity-provider users are deliberately left alone so a
            // swept account can sign in again and resync.
            var applicationUserIds = await dbContext.ApplicationUsers
                .Where(u => (u.LastSeenAt ?? u.CreatedAt) < inactiveCutoff)
                .Select(u => u.Id)
                .ToListAsync(cancellationToken);

            var purgedCount = 0;

            foreach (var applicationUserId in applicationUserIds)
            {
                await accountPurger.PurgeApplicationUserAsync(applicationUserId, cancellationToken);
                purgedCount++;
            }

            return purgedCount;
        }
    }
}
