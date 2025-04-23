using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MCPBuckle.Services;

namespace MCPBuckle.Middleware
{
    /// <summary>
    /// Middleware that serves the MCP context at the .well-known/mcp-context endpoint.
    /// </summary>
    public class McpContextMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<McpContextMiddleware> _logger;
        private readonly string _path;
        private readonly JsonSerializerOptions _jsonOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="McpContextMiddleware"/> class.
        /// </summary>
        /// <param name="next">The next middleware in the pipeline.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="path">The path to serve the MCP context at.</param>
        public McpContextMiddleware(
            RequestDelegate next,
            ILogger<McpContextMiddleware> logger,
            string path = "/.well-known/mcp-context")
        {
            _next = next;
            _logger = logger;
            _path = path;
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        /// <summary>
        /// Processes the request.
        /// </summary>
        /// <param name="context">The HTTP context.</param>
        /// <param name="contextGenerator">The MCP context generator.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task InvokeAsync(HttpContext context, IContextGenerator contextGenerator)
        {
            // Check if the request is for the MCP context
            if (context.Request.Path.Equals(_path, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation($"Serving MCP context at {_path}");

                try
                {
                    // Generate the MCP context
                    var mcpContext = contextGenerator.GenerateContext();

                    // Set the content type
                    context.Response.ContentType = "application/json";

                    // Serialize the MCP context to JSON
                    await JsonSerializer.SerializeAsync(
                        context.Response.Body,
                        mcpContext,
                        _jsonOptions);

                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error serving MCP context");
                    context.Response.StatusCode = 500;
                    await context.Response.WriteAsync("Error generating MCP context");
                    return;
                }
            }

            // Continue with the next middleware
            await _next(context);
        }
    }
}
