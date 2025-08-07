using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using MCPBuckle.Middleware;
using MCPBuckle.Services;
using MCPBuckle.Models;

namespace MCPBuckle.Tests.Middleware
{
    /// <summary>
    /// Tests for the /api/discovery/tools endpoint added in MCPBuckle 1.5.2
    /// to support MCPInvoke 1.4.0+ compatibility.
    /// </summary>
    public class McpDiscoveryEndpointTests
    {
        private readonly Mock<ILogger<McpContextMiddleware>> _loggerMock;
        private readonly Mock<IContextGenerator> _contextGeneratorMock;
        private readonly McpContextMiddleware _middleware;

        public McpDiscoveryEndpointTests()
        {
            _loggerMock = new Mock<ILogger<McpContextMiddleware>>();
            _contextGeneratorMock = new Mock<IContextGenerator>();

            // Create middleware with next delegate that should not be called for our test endpoints
            _middleware = new McpContextMiddleware(
                next: (context) => Task.CompletedTask,
                logger: _loggerMock.Object,
                path: "/.well-known/mcp-context");
        }

        [Fact]
        public async Task InvokeAsync_DiscoveryToolsEndpoint_ReturnsToolsArray()
        {
            // Arrange
            var context = CreateHttpContext("GET", "/api/discovery/tools");
            
            var sampleTools = new List<McpTool>
            {
                new McpTool
                {
                    Name = "CustomerController_GetCustomer",
                    Description = "Gets a customer by ID",
                    InputSchema = new McpSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, McpSchema>
                        {
                            ["id"] = new McpSchema 
                            { 
                                Type = "integer", 
                                Description = "Route parameter id",
                                Annotations = new Dictionary<string, object>
                                {
                                    ["source"] = "route"
                                }
                            }
                        },
                        Required = new List<string> { "id" }
                    }
                },
                new McpTool
                {
                    Name = "OrderController_CreateOrder",
                    Description = "Creates a new order",
                    InputSchema = new McpSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, McpSchema>
                        {
                            ["request"] = new McpSchema 
                            { 
                                Type = "object", 
                                Description = "Complex object of type CreateOrderRequest",
                                Annotations = new Dictionary<string, object>
                                {
                                    ["source"] = "body",
                                    ["properties"] = new Dictionary<string, object>
                                    {
                                        ["CustomerId"] = new Dictionary<string, object> { ["type"] = "integer" },
                                        ["Items"] = new Dictionary<string, object> { ["type"] = "array" }
                                    }
                                }
                            }
                        },
                        Required = new List<string> { "request" }
                    }
                }
            };

            var mcpContext = new McpContext
            {
                Tools = sampleTools,
                Info = new McpInfo
                {
                    Title = "Test API",
                    Description = "Test API v1.0.0",
                    SchemaVersion = "1.0"
                }
            };

            _contextGeneratorMock.Setup(x => x.GenerateContext())
                .Returns(mcpContext);

            // Act
            await _middleware.InvokeAsync(context, _contextGeneratorMock.Object);

            // Assert
            Assert.Equal(200, context.Response.StatusCode);
            Assert.Equal("application/json", context.Response.ContentType);

            var responseBody = GetResponseBody(context);
            var responseJson = JsonDocument.Parse(responseBody);

            // Should return a tools array wrapper for MCPInvoke compatibility
            Assert.True(responseJson.RootElement.TryGetProperty("tools", out var toolsElement));
            Assert.Equal(JsonValueKind.Array, toolsElement.ValueKind);
            Assert.Equal(2, toolsElement.GetArrayLength());

            // Verify first tool structure
            var firstTool = toolsElement[0];
            Assert.True(firstTool.TryGetProperty("name", out _));
            Assert.Equal("CustomerController_GetCustomer", firstTool.GetProperty("name").GetString());
            
            Assert.True(firstTool.TryGetProperty("description", out _));
            Assert.Equal("Gets a customer by ID", firstTool.GetProperty("description").GetString());
            
            Assert.True(firstTool.TryGetProperty("inputSchema", out var inputSchemaElement));
            Assert.True(inputSchemaElement.TryGetProperty("properties", out var propertiesElement));
            Assert.True(propertiesElement.TryGetProperty("id", out var idElement));
            
            // Verify route parameter annotations (key feature for MCPInvoke 1.4.0+ compatibility)
            Assert.True(idElement.TryGetProperty("annotations", out var annotationsElement));
            Assert.True(annotationsElement.TryGetProperty("source", out var sourceElement));
            Assert.Equal("route", sourceElement.GetString());
        }

        [Fact]
        public async Task InvokeAsync_DiscoveryToolsEndpoint_HandlesComplexObjectSchemas()
        {
            // Arrange
            var context = CreateHttpContext("GET", "/api/discovery/tools");
            
            var complexTool = new McpTool
            {
                Name = "Workflow3Controller_UpdateStepDefinition",
                Description = "Updates a step definition with complex nested objects",
                InputSchema = new McpSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, McpSchema>
                    {
                        ["stepDefinitionId"] = new McpSchema 
                        { 
                            Type = "integer", 
                            Description = "Route parameter stepDefinitionId",
                            Annotations = new Dictionary<string, object>
                            {
                                ["source"] = "route"
                            }
                        },
                        ["request"] = new McpSchema 
                        { 
                            Type = "object", 
                            Description = "Complex object of type TestUpdateRequest",
                            Annotations = new Dictionary<string, object>
                            {
                                ["source"] = "body",
                                ["properties"] = new Dictionary<string, object>
                                {
                                    ["StepCode"] = new Dictionary<string, object> 
                                    { 
                                        ["type"] = "string",
                                        ["description"] = "StepCode"
                                    },
                                    ["Definition"] = new Dictionary<string, object> 
                                    { 
                                        ["type"] = "object",
                                        ["description"] = "Definition",
                                        ["properties"] = new Dictionary<string, object>
                                        {
                                            ["StepCode"] = new Dictionary<string, object> { ["type"] = "string" },
                                            ["Processor"] = new Dictionary<string, object> { ["type"] = "object" }
                                        }
                                    }
                                },
                                ["required"] = new List<string> { "StepCode" }
                            }
                        }
                    },
                    Required = new List<string> { "stepDefinitionId", "request" }
                }
            };

            var mcpContext = new McpContext
            {
                Tools = new List<McpTool> { complexTool },
                Info = new McpInfo { Title = "Workflow API", Description = "Workflow API v1.0.0", SchemaVersion = "1.0" }
            };

            _contextGeneratorMock.Setup(x => x.GenerateContext())
                .Returns(mcpContext);

            // Act
            await _middleware.InvokeAsync(context, _contextGeneratorMock.Object);

            // Assert
            Assert.Equal(200, context.Response.StatusCode);

            var responseBody = GetResponseBody(context);
            var responseJson = JsonDocument.Parse(responseBody);

            Assert.True(responseJson.RootElement.TryGetProperty("tools", out var toolsElement));
            var tool = toolsElement[0];
            
            // Verify complex nested structure is preserved
            var inputSchema = tool.GetProperty("inputSchema");
            var properties = inputSchema.GetProperty("properties");
            
            // Check route parameter
            var routeParam = properties.GetProperty("stepDefinitionId");
            Assert.Equal("integer", routeParam.GetProperty("type").GetString());
            Assert.Equal("route", routeParam.GetProperty("annotations").GetProperty("source").GetString());
            
            // Check complex body parameter
            var bodyParam = properties.GetProperty("request");
            Assert.Equal("object", bodyParam.GetProperty("type").GetString());
            Assert.Equal("body", bodyParam.GetProperty("annotations").GetProperty("source").GetString());
            
            // Check nested properties in annotations
            var bodyAnnotations = bodyParam.GetProperty("annotations");
            Assert.True(bodyAnnotations.TryGetProperty("properties", out var nestedProps));
            Assert.True(nestedProps.TryGetProperty("Definition", out var definitionProp));
            Assert.Equal("object", definitionProp.GetProperty("type").GetString());
            Assert.True(definitionProp.TryGetProperty("properties", out _));
        }

        [Fact]
        public async Task InvokeAsync_DiscoveryToolsEndpoint_HandlesEmptyToolsList()
        {
            // Arrange
            var context = CreateHttpContext("GET", "/api/discovery/tools");
            
            var mcpContext = new McpContext
            {
                Tools = new List<McpTool>(),
                Info = new McpInfo { Title = "Empty API", Description = "Empty API v1.0.0", SchemaVersion = "1.0" }
            };

            _contextGeneratorMock.Setup(x => x.GenerateContext())
                .Returns(mcpContext);

            // Act
            await _middleware.InvokeAsync(context, _contextGeneratorMock.Object);

            // Assert
            Assert.Equal(200, context.Response.StatusCode);

            var responseBody = GetResponseBody(context);
            var responseJson = JsonDocument.Parse(responseBody);

            Assert.True(responseJson.RootElement.TryGetProperty("tools", out var toolsElement));
            Assert.Equal(JsonValueKind.Array, toolsElement.ValueKind);
            Assert.Equal(0, toolsElement.GetArrayLength());
        }

        [Fact]
        public async Task InvokeAsync_DiscoveryToolsEndpoint_HandlesGenerationError()
        {
            // Arrange
            var context = CreateHttpContext("GET", "/api/discovery/tools");
            
            _contextGeneratorMock.Setup(x => x.GenerateContext())
                .Throws(new InvalidOperationException("Test error"));

            // Act
            await _middleware.InvokeAsync(context, _contextGeneratorMock.Object);

            // Assert
            Assert.Equal(500, context.Response.StatusCode);
            
            var responseBody = GetResponseBody(context);
            Assert.Contains("Error generating MCP tools discovery", responseBody);
        }

        [Fact]
        public async Task InvokeAsync_DiscoveryToolsEndpoint_LogsInfoMessage()
        {
            // Arrange
            var context = CreateHttpContext("GET", "/api/discovery/tools");
            
            var mcpContext = new McpContext
            {
                Tools = new List<McpTool>(),
                Info = new McpInfo { Title = "Test API", Description = "Test API v1.0.0", SchemaVersion = "1.0" }
            };

            _contextGeneratorMock.Setup(x => x.GenerateContext())
                .Returns(mcpContext);

            // Act
            await _middleware.InvokeAsync(context, _contextGeneratorMock.Object);

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Serving MCP tools discovery at /api/discovery/tools")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task InvokeAsync_WellKnownMcpContext_StillWorksAsExpected()
        {
            // Ensure the original /.well-known/mcp-context endpoint still works
            
            // Arrange
            var context = CreateHttpContext("GET", "/.well-known/mcp-context");
            
            var mcpContext = new McpContext
            {
                Tools = new List<McpTool>
                {
                    new McpTool
                    {
                        Name = "TestController_TestAction",
                        Description = "Test action",
                        InputSchema = new McpSchema { Type = "object" }
                    }
                },
                Info = new McpInfo { Title = "Test API", Description = "Test API v1.0.0", SchemaVersion = "1.0" }
            };

            _contextGeneratorMock.Setup(x => x.GenerateContext())
                .Returns(mcpContext);

            // Act
            await _middleware.InvokeAsync(context, _contextGeneratorMock.Object);

            // Assert
            Assert.Equal(200, context.Response.StatusCode);
            Assert.Equal("application/json", context.Response.ContentType);

            var responseBody = GetResponseBody(context);
            var responseJson = JsonDocument.Parse(responseBody);

            // Should return full MCP context (not just tools array)
            Assert.True(responseJson.RootElement.TryGetProperty("tools", out _));
            Assert.True(responseJson.RootElement.TryGetProperty("info", out _));
            Assert.False(responseJson.RootElement.TryGetProperty("name", out _)); // Different from discovery endpoint format
        }

        [Fact]
        public async Task InvokeAsync_UnrelatedEndpoint_PassesToNextMiddleware()
        {
            // Arrange
            var nextCalled = false;
            var middleware = new McpContextMiddleware(
                next: (context) => { nextCalled = true; return Task.CompletedTask; },
                logger: _loggerMock.Object,
                path: "/.well-known/mcp-context");

            var context = CreateHttpContext("GET", "/api/some-other-endpoint");

            // Act
            await middleware.InvokeAsync(context, _contextGeneratorMock.Object);

            // Assert
            Assert.True(nextCalled);
            Assert.Equal(200, context.Response.StatusCode); // Default from next middleware
        }

        [Theory]
        [InlineData("POST")]
        [InlineData("PUT")]
        [InlineData("DELETE")]
        [InlineData("PATCH")]
        public async Task InvokeAsync_DiscoveryToolsEndpoint_HandlesAllHttpMethods(string httpMethod)
        {
            // The discovery tools endpoint should handle all HTTP methods for maximum compatibility
            
            // Arrange
            var context = CreateHttpContext(httpMethod, "/api/discovery/tools");
            
            var mcpContext = new McpContext
            {
                Tools = new List<McpTool>(),
                Info = new McpInfo { Title = "Test API", Description = "Test API v1.0.0", SchemaVersion = "1.0" }
            };

            _contextGeneratorMock.Setup(x => x.GenerateContext())
                .Returns(mcpContext);

            // Act
            await _middleware.InvokeAsync(context, _contextGeneratorMock.Object);

            // Assert
            Assert.Equal(200, context.Response.StatusCode);
            
            var responseBody = GetResponseBody(context);
            var responseJson = JsonDocument.Parse(responseBody);
            Assert.True(responseJson.RootElement.TryGetProperty("tools", out _));
        }

        #region Helper Methods

        private DefaultHttpContext CreateHttpContext(string method, string path)
        {
            var context = new DefaultHttpContext();
            context.Request.Method = method;
            context.Request.Path = path;
            context.Response.Body = new MemoryStream();
            return context;
        }

        private string GetResponseBody(HttpContext context)
        {
            context.Response.Body.Seek(0, SeekOrigin.Begin);
            using var reader = new StreamReader(context.Response.Body);
            return reader.ReadToEnd();
        }

        #endregion
    }
}