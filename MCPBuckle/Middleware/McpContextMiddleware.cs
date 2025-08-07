using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MCPBuckle.Services;
using MCPBuckle.Models;

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
                    // Check if this is a POST request (potentially JSON-RPC)
                    if (context.Request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
                    {
                        await HandleJsonRpcRequest(context, contextGenerator);
                        return;
                    }
                    
                    // Handle standard GET request for MCP context
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

            // Check if the request is for the discovery tools endpoint (MCPInvoke compatibility)
            if (context.Request.Path.Equals("/api/discovery/tools", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Serving MCP tools discovery at /api/discovery/tools");

                try
                {
                    // Generate the MCP context and extract the tools
                    var mcpContext = contextGenerator.GenerateContext();
                    
                    // Return just the tools array for MCPInvoke compatibility
                    var toolsResponse = new { tools = mcpContext.Tools };

                    // Set the content type
                    context.Response.ContentType = "application/json";

                    // Serialize the tools response to JSON
                    await JsonSerializer.SerializeAsync(
                        context.Response.Body,
                        toolsResponse,
                        _jsonOptions);

                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error serving MCP tools discovery");
                    context.Response.StatusCode = 500;
                    await context.Response.WriteAsync("Error generating MCP tools discovery");
                    return;
                }
            }

            // Continue with the next middleware
            await _next(context);
        }
    
        /// <summary>
        /// Handles a JSON-RPC request to the MCP context endpoint.
        /// </summary>
        /// <param name="context">The HTTP context.</param>
        /// <param name="contextGenerator">The MCP context generator.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task HandleJsonRpcRequest(HttpContext context, IContextGenerator contextGenerator)
        {
            string requestBody;
            using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8))
            {
                requestBody = await reader.ReadToEndAsync();
            }

            _logger.LogInformation($"Received JSON-RPC request: {requestBody}");

            // Parse the JSON-RPC request
            using var requestJson = JsonDocument.Parse(requestBody);
            var root = requestJson.RootElement;

            // Check for valid JSON-RPC 2.0 request
            if (!root.TryGetProperty("jsonrpc", out var jsonrpcVersion) || 
                jsonrpcVersion.GetString() != "2.0" ||
                !root.TryGetProperty("method", out var method))
            {
                await WriteJsonRpcError(context, null, -32600, "Invalid Request");
                return;
            }

            // Get the request ID if it exists
            JsonElement? id = null;
            if (root.TryGetProperty("id", out var idElement))
            {
                id = idElement.Clone();
            }

            // Handle the method
            string methodName = method.GetString() ?? string.Empty;
            _logger.LogInformation($"JSON-RPC method: {methodName}");

            switch (methodName)
            {
                case "tools/list":
                    await HandleToolsListMethod(context, contextGenerator, id);
                    break;
                    
                case "notifications/initialized":
                    // Just acknowledge this notification
                    await WriteJsonRpcResult(context, id, new { success = true });
                    break;

                default:
                    await WriteJsonRpcError(context, id, -32601, $"Method '{methodName}' not found");
                    break;
            }
    }

        /// <summary>
        /// Handles the tools/list JSON-RPC method.
        /// </summary>
        /// <param name="context">The HTTP context.</param>
        /// <param name="contextGenerator">The MCP context generator.</param>
        /// <param name="id">The JSON-RPC request ID.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task HandleToolsListMethod(HttpContext context, IContextGenerator contextGenerator, JsonElement? id)
        {
            try
            {
                // Generate the MCP context and extract the tools
                var mcpContext = contextGenerator.GenerateContext();
                
                // Return the tools in the JSON-RPC response
                await WriteJsonRpcResult(context, id, new { tools = mcpContext.Tools });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling tools/list method");
                await WriteJsonRpcError(context, id, -32603, "Internal error");
            }
        }

        /// <summary>
        /// Writes a JSON-RPC error response to the HTTP context.
        /// </summary>
        /// <param name="context">The HTTP context.</param>
        /// <param name="id">The JSON-RPC request ID.</param>
        /// <param name="code">The error code.</param>
        /// <param name="message">The error message.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task WriteJsonRpcError(HttpContext context, JsonElement? id, int code, string message)
        {
            var error = new
            {
                code = code,
                message = message
            };

            var response = new
            {
                jsonrpc = "2.0",
                id = id,
                error = error
            };

            context.Response.ContentType = "application/json-rpc+json; charset=utf-8";
            await JsonSerializer.SerializeAsync(context.Response.Body, response, _jsonOptions);
        }

        /// <summary>
        /// Writes a JSON-RPC result response to the HTTP context.
        /// </summary>
        /// <param name="context">The HTTP context.</param>
        /// <param name="id">The JSON-RPC request ID.</param>
        /// <param name="result">The result object.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task WriteJsonRpcResult(HttpContext context, JsonElement? id, object result)
        {
            var response = new
            {
                jsonrpc = "2.0",
                id = id,
                result = result
            };

            context.Response.ContentType = "application/json-rpc+json; charset=utf-8";
            await JsonSerializer.SerializeAsync(context.Response.Body, response, _jsonOptions);
        }
    }
}
