using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using MCPBuckle.Middleware;
using MCPBuckle.Models;
using MCPBuckle.Services;

namespace MCPBuckle.Tests
{
    public class McpContextMiddlewareTests
    {
        [Fact]
        public async Task InvokeAsync_WhenPathMatches_ServesMcpContext()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<McpContextMiddleware>>();
            var mockGenerator = new Mock<McpContextGenerator>(
                Mock.Of<Microsoft.AspNetCore.Mvc.Infrastructure.IActionDescriptorCollectionProvider>(),
                Mock.Of<ILogger<McpContextGenerator>>());

            var mcpContext = new McpContext
            {
                Tools = new System.Collections.Generic.List<McpTool>
                {
                    new McpTool { Name = "Test_Tool", Description = "Test tool description" }
                },
                Metadata = new System.Collections.Generic.Dictionary<string, object>
                {
                    ["generator"] = "MCPBuckle"
                }
            };

            mockGenerator.Setup(g => g.GenerateContext()).Returns(mcpContext);

            var middleware = new McpContextMiddleware(
                next: (innerHttpContext) => Task.CompletedTask,
                logger: mockLogger.Object,
                path: "/.well-known/mcp-context");

            var context = new DefaultHttpContext();
            context.Request.Path = "/.well-known/mcp-context";
            context.Response.Body = new MemoryStream();

            // Act
            await middleware.InvokeAsync(context, mockGenerator.Object);

            // Assert
            context.Response.Body.Seek(0, SeekOrigin.Begin);
            var reader = new StreamReader(context.Response.Body);
            var responseBody = await reader.ReadToEndAsync();

            Assert.Equal("application/json", context.Response.ContentType);
            Assert.Contains("Test_Tool", responseBody);
            Assert.Contains("MCPBuckle", responseBody);
        }

        [Fact]
        public async Task InvokeAsync_WhenPathDoesNotMatch_CallsNextMiddleware()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<McpContextMiddleware>>();
            var nextCalled = false;
            
            RequestDelegate next = (innerHttpContext) =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            };

            var middleware = new McpContextMiddleware(
                next: next,
                logger: mockLogger.Object,
                path: "/.well-known/mcp-context");

            var context = new DefaultHttpContext();
            context.Request.Path = "/api/values";

            // Act
            await middleware.InvokeAsync(
                context,
                Mock.Of<McpContextGenerator>());

            // Assert
            Assert.True(nextCalled);
        }
    }
}
