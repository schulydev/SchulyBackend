using Microsoft.EntityFrameworkCore;
using Schuly.Domain.Mcp;

namespace Schuly.Infrastructure.Mcp
{
    /// <summary>The per-connection record of tool calls. It keeps the last <see cref="MaxEntries"/> calls for at most <see cref="Retention"/>, and never what a call sent or got back.</summary>
    public class McpActivityLog(SchulyDbContext db)
    {
        public const int MaxEntries = 100;
        public static readonly TimeSpan Retention = TimeSpan.FromDays(30);

        public async Task RecordAsync(McpCaller caller, string toolName, bool allowed, CancellationToken ct = default)
        {
            if (caller.GrantId is null && caller.ApiKeyId is null)
                return;

            db.McpActivities.Add(new McpActivity { Id = Guid.NewGuid(), ApplicationUserId = caller.ApplicationUserId, McpGrantId = caller.GrantId, McpApiKeyId = caller.ApiKeyId, ToolName = toolName.Length > 100 ? toolName[..100] : toolName, Allowed = allowed });
            await db.SaveChangesAsync(ct);

            // Trimmed in batches rather than on every call, so a busy connection does not pay a delete per tool call.
            var query = Scope(caller.GrantId, caller.ApiKeyId);
            if (await query.CountAsync(ct) <= MaxEntries + 20)
                return;
            var stale = await query.OrderByDescending(a => a.CreatedAt).Skip(MaxEntries).ToListAsync(ct);
            db.McpActivities.RemoveRange(stale);
            await db.SaveChangesAsync(ct);
        }

        public async Task<List<McpActivityEntry>> ListForGrantAsync(Guid applicationUserId, Guid grantId, CancellationToken ct = default) =>
            await List(Scope(grantId, null).Where(a => a.ApplicationUserId == applicationUserId), ct);

        public async Task<List<McpActivityEntry>> ListForApiKeyAsync(Guid applicationUserId, Guid keyId, CancellationToken ct = default) =>
            await List(Scope(null, keyId).Where(a => a.ApplicationUserId == applicationUserId), ct);

        private IQueryable<McpActivity> Scope(Guid? grantId, Guid? keyId) => grantId is { } g
            ? db.McpActivities.Where(a => a.McpGrantId == g)
            : db.McpActivities.Where(a => a.McpApiKeyId == keyId);

        private static async Task<List<McpActivityEntry>> List(IQueryable<McpActivity> query, CancellationToken ct)
        {
            var rows = await query.AsNoTracking().OrderByDescending(a => a.CreatedAt).Take(MaxEntries).ToListAsync(ct);
            return rows.Select(a => new McpActivityEntry(a.ToolName, a.Allowed, a.CreatedAt)).ToList();
        }
    }
}
