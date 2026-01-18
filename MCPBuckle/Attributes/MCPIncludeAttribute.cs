using System;

namespace MCPBuckle.Attributes
{
    /// <summary>
    /// Marks an endpoint for explicit MCP tool inclusion with optional description.
    /// Use for high-frequency endpoints that should be prioritized in discovery.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false)]
    public class MCPIncludeAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the description for this included endpoint.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MCPIncludeAttribute"/> class.
        /// </summary>
        public MCPIncludeAttribute()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MCPIncludeAttribute"/> class with a description.
        /// </summary>
        /// <param name="description">The description for this included endpoint.</param>
        public MCPIncludeAttribute(string? description)
        {
            Description = description;
        }
    }
}
