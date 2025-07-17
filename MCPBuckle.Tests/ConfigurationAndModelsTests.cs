using System;
using System.Collections.Generic;
using Xunit;
using MCPBuckle.Attributes;
using MCPBuckle.Configuration;
using MCPBuckle.Models;

namespace MCPBuckle.Tests
{
    public class ConfigurationAndModelsTests
    {
        [Fact]
        public void McpBuckleOptions_DefaultValues_AreCorrect()
        {
            // Arrange & Act
            var options = new McpBuckleOptions();

            // Assert
            Assert.False(options.IncludeControllerNameInToolName);
            Assert.True(options.IncludeXmlDocumentation);
            Assert.True(options.IncludePropertyDescriptions);
            Assert.Equal("1.0.0", options.SchemaVersion);
            Assert.Equal("API Documentation", options.ServerTitle);
            Assert.Equal("Generated API documentation", options.ServerDescription);
            Assert.NotNull(options.Metadata);
            Assert.Empty(options.Metadata);
            Assert.Null(options.ExcludeControllers);
            Assert.Null(options.IncludeControllers);
            Assert.Null(options.CustomToolNameFactory);
        }

        [Fact]
        public void McpBuckleOptions_CanSetAllProperties()
        {
            // Arrange
            var customMetadata = new Dictionary<string, object> { { "key", "value" } };
            var excludeControllers = new List<string> { "ExcludeMe" };
            var includeControllers = new List<string> { "IncludeMe" };
            Func<string, string, string> customFactory = (controller, action) => $"custom_{controller}_{action}";

            // Act
            var options = new McpBuckleOptions
            {
                IncludeControllerNameInToolName = true,
                IncludeXmlDocumentation = false,
                IncludePropertyDescriptions = false,
                SchemaVersion = "2.0.0",
                ServerTitle = "Custom Title",
                ServerDescription = "Custom Description",
                Metadata = customMetadata,
                ExcludeControllers = excludeControllers,
                IncludeControllers = includeControllers,
                CustomToolNameFactory = customFactory
            };

            // Assert
            Assert.True(options.IncludeControllerNameInToolName);
            Assert.False(options.IncludeXmlDocumentation);
            Assert.False(options.IncludePropertyDescriptions);
            Assert.Equal("2.0.0", options.SchemaVersion);
            Assert.Equal("Custom Title", options.ServerTitle);
            Assert.Equal("Custom Description", options.ServerDescription);
            Assert.Same(customMetadata, options.Metadata);
            Assert.Same(excludeControllers, options.ExcludeControllers);
            Assert.Same(includeControllers, options.IncludeControllers);
            Assert.Same(customFactory, options.CustomToolNameFactory);
        }

        [Fact]
        public void McpContext_DefaultConstructor_InitializesProperties()
        {
            // Act
            var context = new McpContext();

            // Assert
            Assert.NotNull(context.Info);
            Assert.NotNull(context.Tools);
            Assert.Empty(context.Tools);
            Assert.NotNull(context.Metadata);
            Assert.Empty(context.Metadata);
        }

        [Fact]
        public void McpContext_CanSetAllProperties()
        {
            // Arrange
            var info = new McpInfo { SchemaVersion = "1.0", Title = "Test", Description = "Test Description" };
            var tools = new List<McpTool> { new McpTool { Name = "TestTool" } };
            var metadata = new Dictionary<string, object> { { "key", "value" } };

            // Act
            var context = new McpContext
            {
                Info = info,
                Tools = tools,
                Metadata = metadata
            };

            // Assert
            Assert.Same(info, context.Info);
            Assert.Same(tools, context.Tools);
            Assert.Same(metadata, context.Metadata);
        }

        [Fact]
        public void McpInfo_DefaultConstructor_InitializesProperties()
        {
            // Act
            var info = new McpInfo();

            // Assert
            Assert.Equal(string.Empty, info.SchemaVersion);
            Assert.Null(info.Title);
            Assert.Null(info.Description);
        }

        [Fact]
        public void McpInfo_CanSetAllProperties()
        {
            // Act
            var info = new McpInfo
            {
                SchemaVersion = "2.0",
                Title = "Test Title",
                Description = "Test Description"
            };

            // Assert
            Assert.Equal("2.0", info.SchemaVersion);
            Assert.Equal("Test Title", info.Title);
            Assert.Equal("Test Description", info.Description);
        }

        [Fact]
        public void McpTool_DefaultConstructor_InitializesProperties()
        {
            // Act
            var tool = new McpTool();

            // Assert
            Assert.Equal(string.Empty, tool.Name);
            Assert.Equal(string.Empty, tool.Description);  // Description now defaults to empty string, not null
            Assert.NotNull(tool.InputSchema);
            Assert.NotNull(tool.OutputSchema);
            Assert.NotNull(tool.Annotations);
            Assert.Empty(tool.Annotations);
        }

        [Fact]
        public void McpTool_CanSetAllProperties()
        {
            // Arrange
            var inputSchema = new McpSchema { Type = "object" };
            var outputSchema = new McpSchema { Type = "string" };
            var annotations = new Dictionary<string, object> { { "key", "value" } };

            // Act
            var tool = new McpTool
            {
                Name = "TestTool",
                Description = "Test Description",
                InputSchema = inputSchema,
                OutputSchema = outputSchema,
                Annotations = annotations
            };

            // Assert
            Assert.Equal("TestTool", tool.Name);
            Assert.Equal("Test Description", tool.Description);
            Assert.Same(inputSchema, tool.InputSchema);
            Assert.Same(outputSchema, tool.OutputSchema);
            Assert.Same(annotations, tool.Annotations);
        }

        [Fact]
        public void McpSchema_DefaultConstructor_InitializesProperties()
        {
            // Act
            var schema = new McpSchema();

            // Assert
            Assert.Equal(string.Empty, schema.Type);
            Assert.Null(schema.Description);
            Assert.NotNull(schema.Properties);
            Assert.Empty(schema.Properties);
            Assert.NotNull(schema.Required);
            Assert.Empty(schema.Required);
            Assert.Null(schema.Items);
            Assert.Null(schema.Enum);
            Assert.Null(schema.Format);
            Assert.NotNull(schema.AdditionalProperties);
            Assert.Empty(schema.AdditionalProperties);
        }

        [Fact]
        public void McpSchema_CanSetAllProperties()
        {
            // Arrange
            var properties = new Dictionary<string, McpSchema> { { "prop", new McpSchema() } };
            var required = new List<string> { "prop" };
            var items = new McpSchema { Type = "string" };
            var enumValues = new List<object> { "value1", "value2" };
            var additionalProperties = new Dictionary<string, object> { { "key", "value" } };

            // Act
            var schema = new McpSchema
            {
                Type = "object",
                Description = "Test Schema",
                Properties = properties,
                Required = required,
                Items = items,
                Enum = enumValues,
                Format = "date-time",
                AdditionalProperties = additionalProperties
            };

            // Assert
            Assert.Equal("object", schema.Type);
            Assert.Equal("Test Schema", schema.Description);
            Assert.Same(properties, schema.Properties);
            Assert.Same(required, schema.Required);
            Assert.Same(items, schema.Items);
            Assert.Same(enumValues, schema.Enum);
            Assert.Equal("date-time", schema.Format);
            Assert.Same(additionalProperties, schema.AdditionalProperties);
        }

        [Fact]
        public void MCPExcludeAttribute_DefaultConstructor_SetsNullReason()
        {
            // Act
            var attribute = new MCPExcludeAttribute();

            // Assert
            Assert.Null(attribute.Reason);
        }

        [Fact]
        public void MCPExcludeAttribute_WithReason_SetsReason()
        {
            // Arrange
            const string reason = "Test exclusion reason";

            // Act
            var attribute = new MCPExcludeAttribute(reason);

            // Assert
            Assert.Equal(reason, attribute.Reason);
        }

        [Fact]
        public void MCPExcludeAttribute_CanSetReason()
        {
            // Arrange
            const string reason = "Updated reason";

            // Act
            var attribute = new MCPExcludeAttribute
            {
                Reason = reason
            };

            // Assert
            Assert.Equal(reason, attribute.Reason);
        }

        [Fact]
        public void MCPExcludeAttribute_IsAttribute()
        {
            // Act
            var attribute = new MCPExcludeAttribute();

            // Assert
            Assert.IsAssignableFrom<Attribute>(attribute);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("Valid reason")]
        [InlineData(null)]
        public void MCPExcludeAttribute_AcceptsVariousReasonValues(string reason)
        {
            // Act
            var attribute = new MCPExcludeAttribute(reason);

            // Assert
            Assert.Equal(reason, attribute.Reason);
        }
    }
}