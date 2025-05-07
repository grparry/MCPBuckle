using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using MCPBuckle.Configuration;
using MCPBuckle.Models;
using MCPBuckle.Services;

namespace MCPBuckle.Tests
{
    public class McpContextGeneratorTests
    {
        [Fact]
        public void GenerateContext_ReturnsValidContext()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<McpContextGenerator>>();
            var mockDiscoveryService = new Mock<IControllerDiscoveryService>();
            var mockXmlDocumentationService = new Mock<XmlDocumentationService>(MockBehavior.Loose);
            var options = Options.Create(new McpBuckleOptions
            {
                SchemaVersion = "1.0",
                ServerTitle = "Test API"
            });
            var typeSchemaGenerator = new TypeSchemaGenerator(mockXmlDocumentationService.Object, options);
            
            // Setup the discovery service to return a list of tools
            mockDiscoveryService.Setup(d => d.DiscoverTools()).Returns(new List<McpTool>
            {
                new McpTool { Name = "Test_Tool", Description = "Test tool description" }
            });
            
            var generator = new McpContextGenerator(
                mockDiscoveryService.Object,
                mockLogger.Object,
                options);

            // Act
            var context = generator.GenerateContext();

            // Assert
            Assert.NotNull(context);
            Assert.NotNull(context.Tools);
            Assert.Single(context.Tools);
            Assert.Equal("Test_Tool", context.Tools[0].Name);
            Assert.NotNull(context.Metadata);
            Assert.True(context.Metadata.ContainsKey("generator"));
            Assert.Equal("MCPBuckle", context.Metadata["generator"]);
        }

        [Fact]
        public void GenerateContext_CachesResult()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<McpContextGenerator>>();
            var mockDiscoveryService = new Mock<IControllerDiscoveryService>();
            var options = Options.Create(new McpBuckleOptions
            {
                SchemaVersion = "1.0",
                ServerTitle = "Test API"
            });
            
            // Setup the discovery service to return a list of tools
            mockDiscoveryService.Setup(d => d.DiscoverTools()).Returns(new List<McpTool>
            {
                new McpTool { Name = "Test_Tool", Description = "Test tool description" }
            });
            
            var generator = new McpContextGenerator(
                mockDiscoveryService.Object,
                mockLogger.Object,
                options);

            // Act
            var context1 = generator.GenerateContext();
            var context2 = generator.GenerateContext();

            // Assert
            Assert.Same(context1, context2); // Should be the same instance due to caching
            mockDiscoveryService.Verify(d => d.DiscoverTools(), Times.Once);
        }

        [Fact]
        public void InvalidateCache_ClearsCache()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<McpContextGenerator>>();
            var mockDiscoveryService = new Mock<IControllerDiscoveryService>();
            var options = Options.Create(new McpBuckleOptions
            {
                SchemaVersion = "1.0",
                ServerTitle = "Test API"
            });
            
            // Setup the discovery service to return a list of tools
            mockDiscoveryService.Setup(d => d.DiscoverTools()).Returns(new List<McpTool>
            {
                new McpTool { Name = "Test_Tool", Description = "Test tool description" }
            });
            
            var generator = new McpContextGenerator(
                mockDiscoveryService.Object,
                mockLogger.Object,
                options);

            // Act - Get context, invalidate cache, get context again
            var context1 = generator.GenerateContext();
            generator.InvalidateCache();
            var context2 = generator.GenerateContext();

            // Assert
            Assert.NotSame(context1, context2); // Should be different instances after cache invalidation
            mockDiscoveryService.Verify(d => d.DiscoverTools(), Times.Exactly(2));
        }
    }
}
