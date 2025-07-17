using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;
using MCPBuckle.Attributes;
using MCPBuckle.Configuration;
using MCPBuckle.Extensions;
using MCPBuckle.Models;

namespace MCPBuckle.Tests
{
    // TODO: Integration tests need proper WebApplicationFactory setup
    // The current approach causes "multiple entry points" error in test projects
    // Recommended fix: Create a separate test host project or use TestServer directly
    /*
    public class IntegrationTests : IClassFixture<IntegrationTestWebApplicationFactory>
    {
        private readonly IntegrationTestWebApplicationFactory _factory;
        private readonly HttpClient _client;

        public IntegrationTests(IntegrationTestWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task McpContext_Endpoint_ReturnsValidJson()
        {
            // Act
            var response = await _client.GetAsync("/.well-known/mcp-context");

            // Assert
            response.EnsureSuccessStatusCode();
            Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

            var json = await response.Content.ReadAsStringAsync();
            Assert.NotEmpty(json);

            // Verify it's valid JSON
            var context = JsonSerializer.Deserialize<McpContext>(json);
            Assert.NotNull(context);
            Assert.NotNull(context.Tools);
        }

        [Fact]
        public async Task McpContext_ContainsExpectedControllers()
        {
            // Act
            var response = await _client.GetAsync("/.well-known/mcp-context");
            var json = await response.Content.ReadAsStringAsync();
            var context = JsonSerializer.Deserialize<McpContext>(json);

            // Assert
            Assert.NotNull(context?.Tools);
            Assert.NotEmpty(context.Tools);

            // Should contain tools from IntegrationTestController
            var toolNames = context.Tools.Select(t => t.Name).ToList();
            Assert.Contains("GetData", toolNames);
            Assert.Contains("PostData", toolNames);
            
            // Should NOT contain excluded tools
            Assert.DoesNotContain("ExcludedAction", toolNames);
        }

        [Fact]
        public async Task McpContext_ExcludesControllersWithAttribute()
        {
            // Act
            var response = await _client.GetAsync("/.well-known/mcp-context");
            var json = await response.Content.ReadAsStringAsync();
            var context = JsonSerializer.Deserialize<McpContext>(json);

            // Assert
            Assert.NotNull(context?.Tools);
            
            // Should NOT contain tools from ExcludedIntegrationController
            var toolNames = context.Tools.Select(t => t.Name).ToList();
            Assert.DoesNotContain("ExcludedControllerAction", toolNames);
        }

        [Fact]
        public async Task McpContext_IncludesCorrectMetadata()
        {
            // Act
            var response = await _client.GetAsync("/.well-known/mcp-context");
            var json = await response.Content.ReadAsStringAsync();
            var context = JsonSerializer.Deserialize<McpContext>(json);

            // Assert
            Assert.NotNull(context?.Metadata);
            Assert.True(context.Metadata.ContainsKey("generator"));
            Assert.Equal("MCPBuckle", context.Metadata["generator"].ToString());
        }

        [Fact]
        public async Task McpContext_WithCustomPath_ReturnsNotFound()
        {
            // Act
            var response = await _client.GetAsync("/custom-mcp-path");

            // Assert
            Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task McpContext_HasCorrectToolStructure()
        {
            // Act
            var response = await _client.GetAsync("/.well-known/mcp-context");
            var json = await response.Content.ReadAsStringAsync();
            var context = JsonSerializer.Deserialize<McpContext>(json);

            // Assert
            Assert.NotNull(context?.Tools);
            var tool = context.Tools.FirstOrDefault(t => t.Name == "GetData");
            Assert.NotNull(tool);
            
            Assert.NotNull(tool.InputSchema);
            Assert.NotNull(tool.OutputSchema);
            Assert.NotNull(tool.Annotations);
            Assert.Contains("HandlerTypeAssemblyQualifiedName", tool.Annotations.Keys);
            Assert.Contains("MethodName", tool.Annotations.Keys);
        }

        [Fact]
        public async Task McpContext_WithComplexParameters_GeneratesCorrectSchema()
        {
            // Act
            var response = await _client.GetAsync("/.well-known/mcp-context");
            var json = await response.Content.ReadAsStringAsync();
            var context = JsonSerializer.Deserialize<McpContext>(json);

            // Assert
            Assert.NotNull(context?.Tools);
            var tool = context.Tools.FirstOrDefault(t => t.Name == "PostData");
            Assert.NotNull(tool);
            
            Assert.NotNull(tool.InputSchema);
            Assert.Equal("object", tool.InputSchema.Type);
            Assert.NotNull(tool.InputSchema.Properties);
            
            // Check for body parameter preservation
            Assert.True(tool.InputSchema.Properties.ContainsKey("model"));
        }

        [Fact]
        public async Task NonMcpEndpoint_ReturnsExpectedResponse()
        {
            // Act
            var response = await _client.GetAsync("/api/integration-test/data");

            // Assert
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            Assert.Equal("\"Test data\"", content);
        }
    }

    public class IntegrationTestWebApplicationFactory : WebApplicationFactory<IntegrationTestWebApplicationFactory>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.AddControllers();
                services.AddMcpBuckle(options =>
                {
                    options.SchemaVersion = "1.0.0";
                    options.ServerTitle = "Integration Test API";
                    options.ServerDescription = "Test API for integration testing";
                    options.IncludeControllerNameInToolName = false;
                });
            });

            builder.Configure(app =>
            {
                app.UseRouting();
                app.UseMcpBuckle();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapControllers();
                });
            });
        }

        protected override IHost CreateHost(IHostBuilder builder)
        {
            // Override to ensure we don't look for a Main method
            builder.ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseMcpBuckle();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapControllers();
                    });
                });
            });

            return base.CreateHost(builder);
        }
    }

    [ApiController]
    [Route("api/integration-test")]
    public class IntegrationTestController : ControllerBase
    {
        /// <summary>
        /// Gets test data
        /// </summary>
        /// <param name="id">The data ID</param>
        /// <returns>Test data</returns>
        [HttpGet("data")]
        public IActionResult GetData(int id = 1)
        {
            return Ok("Test data");
        }

        /// <summary>
        /// Posts test data
        /// </summary>
        /// <param name="model">The test model</param>
        /// <returns>Success response</returns>
        [HttpPost("data")]
        public IActionResult PostData([FromBody] IntegrationTestModel model)
        {
            return Ok(new { success = true, receivedId = model.Id });
        }

        /// <summary>
        /// Action with complex parameters
        /// </summary>
        /// <param name="id">The ID</param>
        /// <param name="name">The name</param>
        /// <param name="active">Whether active</param>
        /// <returns>Complex response</returns>
        [HttpGet("complex")]
        public IActionResult GetComplex(int id, string name, bool active = true)
        {
            return Ok(new { id, name, active });
        }

        /// <summary>
        /// Excluded action
        /// </summary>
        /// <returns>Should not appear in MCP</returns>
        [MCPExclude("This action is excluded from MCP")]
        [HttpGet("excluded")]
        public IActionResult ExcludedAction()
        {
            return Ok("Excluded");
        }

        /// <summary>
        /// Async action
        /// </summary>
        /// <returns>Async result</returns>
        [HttpGet("async")]
        public async Task<IActionResult> GetAsync()
        {
            await Task.Delay(1);
            return Ok("Async result");
        }
    }

    [MCPExclude("Entire controller is excluded")]
    [ApiController]
    [Route("api/excluded")]
    public class ExcludedIntegrationController : ControllerBase
    {
        [HttpGet("action")]
        public IActionResult ExcludedControllerAction()
        {
            return Ok("Should not appear");
        }
    }

    public class IntegrationTestModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public List<string> Tags { get; set; } = new();
    }
    */
}