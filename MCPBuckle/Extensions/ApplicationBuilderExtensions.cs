using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Options;
using MCPBuckle.Configuration;
using MCPBuckle.Middleware;

namespace MCPBuckle.Extensions
{
    /// <summary>
    /// Extension methods for <see cref="IApplicationBuilder"/> to add MCPBuckle middleware.
    /// </summary>
    public static class ApplicationBuilderExtensions
    {
        /// <summary>
        /// Adds the MCPBuckle middleware to the application pipeline.
        /// </summary>
        /// <param name="app">The application builder.</param>
        /// <returns>The application builder for chaining.</returns>
        public static IApplicationBuilder UseMcpBuckle(this IApplicationBuilder app)
        {
            // Get the options from the service provider
            var options = app.ApplicationServices.GetService(typeof(IOptions<McpBuckleOptions>)) as IOptions<McpBuckleOptions>;
            var path = options?.Value.RoutePrefix ?? "/.well-known/mcp-context";

            // Add the middleware to the pipeline
            app.UseMiddleware<McpContextMiddleware>(path);

            return app;
        }

        /// <summary>
        /// Adds the MCPBuckle middleware to the application pipeline with a custom path.
        /// </summary>
        /// <param name="app">The application builder.</param>
        /// <param name="path">The path to serve the MCP context at.</param>
        /// <returns>The application builder for chaining.</returns>
        public static IApplicationBuilder UseMcpBuckle(
            this IApplicationBuilder app,
            string path)
        {
            // Add the middleware to the pipeline
            app.UseMiddleware<McpContextMiddleware>(path);

            return app;
        }
    }
}
