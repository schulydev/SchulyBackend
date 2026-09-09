namespace Schuly.Infrastructure.Services
{
    public class RetentionOptions
    {
        public const string SectionName = "Retention";

        public bool Enabled { get; set; } = true;
        public int MonthsAfterLeave { get; set; } = 6;
        public int MonthsInactive { get; set; } = 12;
    }
}
