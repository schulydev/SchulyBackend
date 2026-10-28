namespace Schuly.Domain
{
    public class PluginTask : Base
    {
        public required string PluginName { get; set; }
        public required string TaskName { get; set; }
        public required string Cron { get; set; }
        public DateTime? NextRunAt { get; set; }
        public string LastStatus { get; set; } = "Pending";
        public DateTime? LastStartedAt { get; set; }
        public DateTime? LastFinishedAt { get; set; }
        public long? LastDurationMs { get; set; }
        public string? LastError { get; set; }
        public int TotalRuns { get; set; }
        public int TotalFailures { get; set; }
        public int ConsecutiveFailures { get; set; }
    }
}
