using System;
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
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
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
        /// Gets or sets annotations for the schema. Used for parameter source information and detailed properties.
        /// </summary>
        [JsonPropertyName("annotations")]
        public Dictionary<string, object>? Annotations { get; set; }

        /// <summary>
        /// Gets or sets the oneOf schemas for union types (polymorphic types).
        /// </summary>
        [JsonPropertyName("oneOf")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<McpSchema>? OneOf { get; set; }

        /// <summary>
        /// Gets or sets the discriminator information for polymorphic types.
        /// </summary>
        [JsonPropertyName("discriminator")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public McpDiscriminator? Discriminator { get; set; }

        /// <summary>
        /// Gets or sets the schema reference (e.g., "#/$defs/TypeName").
        /// </summary>
        [JsonPropertyName("$ref")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Ref { get; set; }

        /// <summary>
        /// Gets or sets a constant value for discriminator properties.
        /// </summary>
        [JsonPropertyName("const")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object? Const { get; set; }

        /// <summary>
        /// Gets or sets the schema definitions ($defs section).
        /// </summary>
        [JsonPropertyName("$defs")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, McpSchema>? Definitions { get; set; }

        /// <summary>
        /// Gets or sets the parameter source (route, body, query, header) for MCPInvoke 1.4.0+ compatibility.
        /// </summary>
        [JsonIgnore]
        public string? Source { get; set; }

        /// <summary>
        /// Gets or sets whether this parameter is required.
        /// </summary>
        [JsonIgnore]
        public bool IsRequired { get; set; }

        /// <summary>
        /// Gets or sets the source .NET type for this schema. Used during code generation.
        /// </summary>
        [JsonIgnore]
        public Type? SourceType { get; set; }

        /// <summary>
        /// Gets or sets whether this schema represents a polymorphic base type.
        /// </summary>
        [JsonIgnore]
        public bool IsPolymorphicBase { get; set; }

        /// <summary>
        /// Gets or sets the discriminator value for derived types in polymorphic hierarchies.
        /// </summary>
        [JsonIgnore]
        public string? DiscriminatorValue { get; set; }

        /// <summary>
        /// Gets or sets additional properties for the schema.
        /// </summary>
        [JsonExtensionData]
        public Dictionary<string, object> AdditionalProperties { get; set; } = new Dictionary<string, object>();
    }
}
