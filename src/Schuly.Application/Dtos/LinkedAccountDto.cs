namespace Schuly.Application.Dtos
{
    public class LinkedAccountDto
    {
        public Guid Id { get; set; }
        public required string Provider { get; set; }
        public required string Email { get; set; }
        public string? DisplayName { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
