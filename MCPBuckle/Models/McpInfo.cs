using System.Text.Json.Serialization;

namespace MCPBuckle.Models
{
    /// <summary>
    /// Represents the info section of the MCP context document
    /// </summary>
    public class McpInfo
    {
        /// <summary>
        /// Gets or sets the MCP schema version being used
        /// </summary>
        [JsonPropertyName("schema_version")]
        public string SchemaVersion { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the title of the MCP server
        /// </summary>
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        /// <summary>
        /// Gets or sets the description of the MCP server
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; set; }
    }
}
