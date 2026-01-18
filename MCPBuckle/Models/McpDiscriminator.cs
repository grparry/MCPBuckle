using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MCPBuckle.Models
{
    /// <summary>
    /// Represents a discriminator for polymorphic types in OpenAPI/JSON Schema format.
    /// Used to map discriminator values to specific schema types.
    /// </summary>
    public class McpDiscriminator
    {
        /// <summary>
        /// Gets or sets the name of the property that acts as the discriminator.
        /// </summary>
        [JsonPropertyName("propertyName")]
        public string PropertyName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the mapping of discriminator values to schema references.
        /// Key is the discriminator value, value is the schema reference (e.g., "#/$defs/TypeName").
        /// </summary>
        [JsonPropertyName("mapping")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, string>? Mapping { get; set; }
    }
}
