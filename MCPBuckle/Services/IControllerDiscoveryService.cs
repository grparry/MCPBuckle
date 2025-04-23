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
        /// <returns>A list of MCP tools representing the API endpoints.</returns>
        List<McpTool> DiscoverTools();
    }
}
