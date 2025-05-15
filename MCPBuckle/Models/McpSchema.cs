using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MCPBuckle.Models
{
    /// <summary>
    /// Represents a JSON Schema in the Model Context Protocol (MCP) format.
    /// </summary>
    public class McpSchema
    {
        /// <summary>
        /// Gets or sets the type of the schema.
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the properties of the schema when Type is "object".
        /// </summary>
        [JsonPropertyName("properties")]
        public Dictionary<string, McpSchema> Properties { get; set; } = new Dictionary<string, McpSchema>();

        /// <summary>
        /// Gets or sets the required properties when Type is "object".
        /// </summary>
        [JsonPropertyName("required")]
        public List<string> Required { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the items schema when Type is "array".
        /// </summary>
        [JsonPropertyName("items")]
        public McpSchema? Items { get; set; }

        /// <summary>
        /// Gets or sets the description of the schema.
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the format of the schema (e.g., "date-time", "email", etc.).
        /// </summary>
        [JsonPropertyName("format")]
        public string? Format { get; set; }
        
        /// <summary>
        /// Gets or sets the enum values for enum types.
        /// </summary>
        [JsonPropertyName("enum")]
        public List<object>? Enum { get; set; }

        /// <summary>
        /// Gets or sets additional properties for the schema.
        /// </summary>
        [JsonExtensionData]
        public Dictionary<string, object> AdditionalProperties { get; set; } = new Dictionary<string, object>();
    }
}
