using System;

namespace MCPBuckle.Attributes
{
    /// <summary>
    /// Attribute to exclude a controller or action from MCP tool discovery.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false)]
    public class MCPExcludeAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the reason for excluding this controller or action.
        /// </summary>
        public string? Reason { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MCPExcludeAttribute"/> class.
        /// </summary>
        public MCPExcludeAttribute()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MCPExcludeAttribute"/> class with a reason.
        /// </summary>
        /// <param name="reason">The reason for excluding this controller or action.</param>
        public MCPExcludeAttribute(string? reason)
        {
            Reason = reason;
        }
    }
}
