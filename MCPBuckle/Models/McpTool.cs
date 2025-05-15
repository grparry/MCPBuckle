using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MCPBuckle.Models
{
    /// <summary>
    /// Represents a tool in the Model Context Protocol (MCP) format.
    /// </summary>
    public class McpTool
    {
        /// <summary>
        /// Gets or sets the name of the tool. This is typically derived from the operationId in OpenAPI.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the description of the tool. This is typically derived from the summary or description in OpenAPI.
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the input schema for the tool. This defines the parameters that the tool accepts.
        /// </summary>
        [JsonPropertyName("inputSchema")]
        public McpSchema InputSchema { get; set; } = new McpSchema();

        /// <summary>
        /// Gets or sets the output schema for the tool. This defines the structure of the response that the tool returns.
        /// </summary>
        [JsonPropertyName("outputSchema")]
        public McpSchema OutputSchema { get; set; } = new McpSchema();

        /// <summary>
        /// Gets or sets additional annotations for the tool. This can include metadata from the original OpenAPI spec.
        /// </summary>
        [JsonPropertyName("annotations")]
        public Dictionary<string, object> Annotations { get; set; } = new Dictionary<string, object>();
    }
}
