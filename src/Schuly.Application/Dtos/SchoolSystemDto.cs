namespace Schuly.Application.Dtos
{
    public class SchoolSystemDto
    {
        public Guid Id { get; set; }
        public required string Key { get; set; }
        public required string DisplayName { get; set; }
        public string? LogoUrl { get; set; }
        public string? PrivateAuthStrategy { get; set; }
        public string? StatelessBasePath { get; set; }
        public string? PluginBasePath { get; set; }
        public required string LoginMethod { get; set; }
        public bool Enabled { get; set; }
        public int SortOrder { get; set; }
        public List<SchoolSystemLoginFieldDto> LoginFields { get; set; } = [];
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class SchoolSystemLoginFieldDto
    {
        public required string Key { get; set; }
        public required string Label { get; set; }
        public required string Type { get; set; }
        public string? Placeholder { get; set; }
        public string? DefaultValue { get; set; }
        public bool Required { get; set; } = true;
    }
}
