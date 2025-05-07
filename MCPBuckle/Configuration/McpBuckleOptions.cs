using System;
using System.Collections.Generic;

namespace MCPBuckle.Configuration
{
    /// <summary>
    /// Configuration options for MCPBuckle.
    /// </summary>
    public class McpBuckleOptions
    {
        /// <summary>
        /// Gets or sets the path where the MCP context will be served.
        /// </summary>
        /// <remarks>
        /// Default value is "/.well-known/mcp-context".
        /// </remarks>
        public string RoutePrefix { get; set; } = "/.well-known/mcp-context";

        /// <summary>
        /// Gets or sets a value indicating whether to include controller name in the tool name.
        /// </summary>
        /// <remarks>
        /// If true, tool names will be in the format "{ControllerName}_{ActionName}".
        /// If false, tool names will be just the action name.
        /// Default value is true.
        /// </remarks>
        public bool IncludeControllerNameInToolName { get; set; } = true;

        /// <summary>
        /// Gets or sets a list of controller names to include. If null or empty, all controllers are included.
        /// </summary>
        public List<string>? IncludeControllers { get; set; }

        /// <summary>
        /// Gets or sets a list of controller names to exclude.
        /// </summary>
        public List<string>? ExcludeControllers { get; set; }

        /// <summary>
        /// Gets or sets a function to customize the tool name.
        /// </summary>
        /// <remarks>
        /// This function takes the controller name and action name and returns a custom tool name.
        /// If null, the default naming convention is used.
        /// </remarks>
        public Func<string, string, string>? CustomToolNameFactory { get; set; }

        /// <summary>
        /// Gets or sets the list of assemblies to scan for controllers and MCP tools.
        /// If null or empty, the entry assembly will be used.
        /// </summary>
        public List<System.Reflection.Assembly>? AssembliesToScan { get; set; }

        /// <summary>
        /// Gets or sets the MCP schema version to use in the info section.
        /// </summary>
        /// <remarks>
        /// This is required by the MCP specification.
        /// </remarks>
        public string SchemaVersion { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the title of the MCP server for the info section.
        /// </summary>
        /// <remarks>
        /// This is required by the MCP specification.
        /// </remarks>
        public string ServerTitle { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the description of the MCP server for the info section.
        /// </summary>
        public string? ServerDescription { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to include XML documentation.
        /// </summary>
        /// <remarks>
        /// Default value is true.
        /// </remarks>
        public bool IncludeXmlDocumentation { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to include property descriptions in schemas.
        /// </summary>
        /// <remarks>
        /// Default value is true.
        /// </remarks>
        public bool IncludePropertyDescriptions { get; set; } = true;

        /// <summary>
        /// Gets or sets the metadata to include in the MCP context.
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
    }
}
