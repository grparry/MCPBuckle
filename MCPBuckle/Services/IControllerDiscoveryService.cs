using System.Collections.Generic;
using MCPBuckle.Models;

namespace MCPBuckle.Services
{
    /// <summary>
    /// Interface for services that discover controllers and their actions in an ASP.NET Core application.
    /// </summary>
    public interface IControllerDiscoveryService
    {
        /// <summary>
        /// Discovers all controllers and their actions in the application and converts them to MCP tools.
        /// </summary>
        /// <param name="ignoreRequireExplicitInclusion">
        /// When true, ignores the RequireExplicitInclusion option and returns all tools except those with MCPExclude.
        /// Used by MCPInvoke to ensure all tools discovered via semantic search are executable.
        /// </param>
        /// <returns>A list of MCP tools representing the API endpoints.</returns>
        List<McpTool> DiscoverTools(bool ignoreRequireExplicitInclusion = false);
    }
}
