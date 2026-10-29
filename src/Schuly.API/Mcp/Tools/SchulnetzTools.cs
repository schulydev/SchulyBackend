using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Schuly.Domain.Mcp;
using static Schuly.API.Mcp.Tools.McpToolSupport;

namespace Schuly.API.Mcp.Tools
{
    /// <summary>Reporting absences to Schulnetz through the Schulware plugin's endpoints, called in process with the caller's principal.</summary>
    [McpServerToolType]
    public sealed class SchulnetzTools(InProcessPluginApi plugins)
    {
        private const string Base = "/api/plugins/schulware/accounts";

        [McpServerTool(Name = "list_reported_absences", Title = "List reported absences", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
        [Description("Lists the absences the student reported in Schulnetz that the school has not processed yet. Only these can be withdrawn.")]
        [McpAccess(McpArea.Absences, McpAccessLevel.Read)]
        public async Task<string> ListReportedAbsences([Description("Schulnetz account from list_school_accounts; defaults to the first one.")] Guid? accountId = null, CancellationToken ct = default)
        {
            var account = await AccountAsync(accountId, ct);
            var reported = Items(Ensure(await plugins.GetAsync($"{Base}/{account}/absences/reported", ct)));
            return Serialize(new { accountId = account, reported = reported.Select(r => Clean(r)) });
        }

        [McpServerTool(Name = "list_absence_reasons", Title = "List absence reasons", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
        [Description("Lists the absence reasons the school accepts in Schulnetz. Use one of them as reason in report_absence.")]
        [McpAccess(McpArea.Absences, McpAccessLevel.Read)]
        public async Task<string> ListAbsenceReasons(Guid? accountId = null, CancellationToken ct = default)
        {
            var account = await AccountAsync(accountId, ct);
            return Serialize(new { accountId = account, reasons = Clean(Ensure(await plugins.GetAsync($"{Base}/{account}/absences/reasons", ct)) ?? default) });
        }

        [McpServerTool(Name = "report_absence", Title = "Report absence to Schulnetz", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = true)]
        [Description("Reports an absence to the school in Schulnetz on the student's behalf. The school sees it immediately, so confirm dates and reason with the user first. Give hourFrom and hourTo together for part of a day.")]
        [McpAccess(McpArea.Absences, McpAccessLevel.ReadWrite)]
        public async Task<string> ReportAbsence([Description("First day, yyyy-MM-dd.")] string dateFrom, [Description("Last day, yyyy-MM-dd.")] string dateTo, [Description("A reason from list_absence_reasons.")] string? reason = null, [Description("Note for the school.")] string? comment = null, [Description("Start time HH:mm, for part of a day.")] string? hourFrom = null, [Description("End time HH:mm.")] string? hourTo = null, Guid? accountId = null, CancellationToken ct = default)
        {
            var account = await AccountAsync(accountId, ct);
            var result = Ensure(await plugins.PostAsync($"{Base}/{account}/absences", new { dateFrom, dateTo, hourFrom, hourTo, reason, comment }, ct));
            return Serialize(new { reported = true, accountId = account, result = result is { } r ? Clean(r) : null });
        }

        [McpServerTool(Name = "withdraw_absence_report", Title = "Withdraw absence report", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = true)]
        [Description("Withdraws (deletes) absences the student reported that the school has not processed yet. Pass the ids from list_reported_absences. Needs the connection's permission for destructive actions.")]
        [McpAccess(McpArea.Absences, McpAccessLevel.ReadWrite)]
        [McpDestructive]
        public async Task<string> WithdrawAbsenceReport([Description("ids of one reported absence, from list_reported_absences.")] string[] ids, Guid? accountId = null, CancellationToken ct = default)
        {
            if (ids.Length is 0 or > 50)
                throw new McpException("Pass between 1 and 50 ids.");
            var account = await AccountAsync(accountId, ct);
            var query = string.Join('&', ids.Select(id => "ids=" + Uri.EscapeDataString(id)));
            Ensure(await plugins.DeleteAsync($"{Base}/{account}/absences?{query}", ct));
            return Serialize(new { withdrawn = ids.Length, accountId = account });
        }

        private async Task<Guid> AccountAsync(Guid? requested, CancellationToken ct)
        {
            var accounts = Items(Ensure(await plugins.GetAsync(Base, ct))).Select(a => Guid.TryParse(Str(a, "id"), out var id) ? id : Guid.Empty).Where(id => id != Guid.Empty).ToList();
            if (requested is { } wanted)
                return accounts.Contains(wanted) ? wanted : throw new McpException("That Schulnetz account is not connected to Schuly.");
            return accounts.FirstOrDefault() is { } first && first != Guid.Empty ? first : throw new McpException("No Schulnetz account is connected to Schuly. Connect one in the Schuly app first.");
        }

        private static JsonElement? Ensure(PluginApiResponse response)
        {
            if (response.IsSuccess)
                return response.Json();
            var message = response.Json() is { } error ? Str(error, "message") : null;
            throw new McpException(response.StatusCode switch
            {
                StatusCodes.Status404NotFound => "Not found. Either the account does not exist or the Schulware plugin is not installed on this Schuly server.",
                StatusCodes.Status400BadRequest => message ?? "Schulnetz rejected the request. Check the dates and reason.",
                _ => message ?? "Schulnetz did not answer. Try again later.",
            });
        }
    }
}
