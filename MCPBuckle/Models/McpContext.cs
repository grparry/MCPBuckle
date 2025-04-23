using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MCPBuckle.Models
{
    /// <summary>
    /// Represents the root Model Context Protocol (MCP) document.
    /// </summary>
    public class McpContext
    {
        /// <summary>
        /// Gets or sets the list of tools available in this MCP context.
        /// </summary>
        [JsonPropertyName("tools")]
        public List<McpTool> Tools { get; set; } = new List<McpTool>();

        /// <summary>
        /// Gets or sets metadata about the API.
        /// </summary>
        [JsonPropertyName("metadata")]
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
    }
}
