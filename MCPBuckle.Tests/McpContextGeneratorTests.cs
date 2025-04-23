using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
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
            var mockActionDescriptorCollectionProvider = SetupMockActionDescriptorCollectionProvider();
            var mockXmlDocumentationService = new Mock<XmlDocumentationService>();
            var mockTypeSchemaGenerator = new Mock<TypeSchemaGenerator>(mockXmlDocumentationService.Object, Options.Create(new McpBuckleOptions()));
            var mockOptions = new Mock<IOptions<McpBuckleOptions>>();
            mockOptions.Setup(o => o.Value).Returns(new McpBuckleOptions());
            
            var generator = new McpContextGenerator(
                mockActionDescriptorCollectionProvider.Object,
                mockXmlDocumentationService.Object,
                mockTypeSchemaGenerator.Object,
                mockLogger.Object,
                mockOptions.Object);

            // Act
            var context = generator.GenerateContext();

            // Assert
            Assert.NotNull(context);
            Assert.NotNull(context.Tools);
            Assert.NotNull(context.Metadata);
            Assert.True(context.Metadata.ContainsKey("generator"));
            Assert.Equal("MCPBuckle", context.Metadata["generator"]);
        }

        [Fact]
        public void GenerateContext_CachesResult()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<McpContextGenerator>>();
            var mockActionDescriptorCollectionProvider = SetupMockActionDescriptorCollectionProvider();
            var mockXmlDocumentationService = new Mock<XmlDocumentationService>();
            var mockTypeSchemaGenerator = new Mock<TypeSchemaGenerator>(mockXmlDocumentationService.Object, Options.Create(new McpBuckleOptions()));
            var mockOptions = new Mock<IOptions<McpBuckleOptions>>();
            mockOptions.Setup(o => o.Value).Returns(new McpBuckleOptions());
            
            var generator = new McpContextGenerator(
                mockActionDescriptorCollectionProvider.Object,
                mockXmlDocumentationService.Object,
                mockTypeSchemaGenerator.Object,
                mockLogger.Object,
                mockOptions.Object);

            // Act
            var context1 = generator.GenerateContext();
            var context2 = generator.GenerateContext();

            // Assert
            Assert.Same(context1, context2); // Should return the same instance (cached)
        }

        [Fact]
        public void InvalidateCache_ClearsCache()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<McpContextGenerator>>();
            var mockActionDescriptorCollectionProvider = SetupMockActionDescriptorCollectionProvider();
            var mockXmlDocumentationService = new Mock<XmlDocumentationService>();
            var mockTypeSchemaGenerator = new Mock<TypeSchemaGenerator>(mockXmlDocumentationService.Object, Options.Create(new McpBuckleOptions()));
            var mockOptions = new Mock<IOptions<McpBuckleOptions>>();
            mockOptions.Setup(o => o.Value).Returns(new McpBuckleOptions());
            
            var generator = new McpContextGenerator(
                mockActionDescriptorCollectionProvider.Object,
                mockXmlDocumentationService.Object,
                mockTypeSchemaGenerator.Object,
                mockLogger.Object,
                mockOptions.Object);

            // Act
            var context1 = generator.GenerateContext();
            generator.InvalidateCache();
            var context2 = generator.GenerateContext();

            // Assert
            Assert.NotSame(context1, context2); // Should return different instances after cache invalidation
        }

        private Mock<IActionDescriptorCollectionProvider> SetupMockActionDescriptorCollectionProvider()
        {
            var mockProvider = new Mock<IActionDescriptorCollectionProvider>();
            var actionDescriptors = new ActionDescriptorCollection(
                new List<ActionDescriptor>
                {
                    CreateMockControllerActionDescriptor("TestController", "Get", "HttpGet")
                },
                0);

            mockProvider.Setup(p => p.ActionDescriptors).Returns(actionDescriptors);
            return mockProvider;
        }

        private ControllerActionDescriptor CreateMockControllerActionDescriptor(
            string controllerName,
            string actionName,
            string httpMethod)
        {
            var descriptor = new ControllerActionDescriptor
            {
                ControllerName = controllerName,
                ActionName = actionName,
                DisplayName = $"{controllerName}.{actionName}"
            };

            // Add a mock method info with HTTP method attribute
            var mockMethodInfo = new Mock<System.Reflection.MethodInfo>();
            var mockHttpAttribute = new Mock<Attribute>();
            mockHttpAttribute.Setup(a => a.GetType().Name).Returns($"{httpMethod}Attribute");
            
            mockMethodInfo.Setup(m => m.GetCustomAttributes(It.IsAny<bool>()))
                .Returns(new[] { mockHttpAttribute.Object });

            descriptor.MethodInfo = mockMethodInfo.Object;

            return descriptor;
        }
    }
}
