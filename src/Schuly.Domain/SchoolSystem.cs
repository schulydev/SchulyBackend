namespace Schuly.Domain
{
    public class SchoolSystem : Base
    {
        public required string Key { get; set; }

        public required string DisplayName { get; set; }
        public string? LogoUrl { get; set; }

        public string? PrivateAuthStrategy { get; set; }

        public string? StatelessBasePath { get; set; }

        public string? PluginBasePath { get; set; }

        public required string LoginMethod { get; set; }

        public bool Enabled { get; set; } = true;

        public int SortOrder { get; set; }

        public List<SchoolSystemLoginField> LoginFields { get; set; } = [];
    }

    public class SchoolSystemLoginField
    {
        public required string Key { get; set; }

        public required string Label { get; set; }

        public required string Type { get; set; }

        public string? Placeholder { get; set; }
        public string? DefaultValue { get; set; }
        public bool Required { get; set; } = true;
    }
}
