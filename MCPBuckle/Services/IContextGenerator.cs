using MCPBuckle.Models;

namespace MCPBuckle.Services
{
    /// <summary>
    /// Interface for services that generate MCP context
    /// </summary>
    public interface IContextGenerator
    {
        /// <summary>
        /// Generates an MCP context containing tools and metadata
        /// </summary>
        /// <returns>The generated MCP context</returns>
        McpContext GenerateContext();

        /// <summary>
        /// Invalidates any cached context
        /// </summary>
        void InvalidateCache();
    }
}
