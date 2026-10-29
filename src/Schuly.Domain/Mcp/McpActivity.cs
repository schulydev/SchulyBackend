namespace Schuly.Domain.Mcp
{
    /// <summary>One tool call by a connection: which tool, when, and whether its permissions allowed it. Never the arguments or the result.</summary>
    public class McpActivity : Base
    {
        public Guid ApplicationUserId { get; set; }
        public ApplicationUser? ApplicationUser { get; set; }

        public Guid? McpGrantId { get; set; }
        public Guid? McpApiKeyId { get; set; }

        public required string ToolName { get; set; }
        public bool Allowed { get; set; }
    }
}
