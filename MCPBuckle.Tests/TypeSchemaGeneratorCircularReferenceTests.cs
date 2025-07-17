using System;
using System.Collections.Generic;
using Microsoft.Extensions.Options;
using Xunit;
using MCPBuckle.Configuration;
using MCPBuckle.Services;

namespace MCPBuckle.Tests
{
    /// <summary>
    /// Tests for circular reference handling in TypeSchemaGenerator.
    /// </summary>
    public class TypeSchemaGeneratorCircularReferenceTests
    {
        private readonly TypeSchemaGenerator _typeSchemaGenerator;

        public TypeSchemaGeneratorCircularReferenceTests()
        {
            var xmlDocService = new XmlDocumentationService();
            var options = Options.Create(new McpBuckleOptions
            {
                IncludePropertyDescriptions = true,
                IncludeXmlDocumentation = true
            });
            _typeSchemaGenerator = new TypeSchemaGenerator(xmlDocService, options);
        }

        [Fact]
        public void GenerateSchema_WithCircularReference_ShouldNotCauseStackOverflow()
        {
            // Arrange - CircularA and CircularB reference each other
            var typeA = typeof(CircularA);

            // Act & Assert - This should not throw a StackOverflowException
            var schema = _typeSchemaGenerator.GenerateSchema(typeA);

            // Verify the schema was generated
            Assert.NotNull(schema);
            Assert.Equal("object", schema.Type);
            Assert.NotNull(schema.Properties);
        }

        [Fact]
        public void GenerateSchema_WithSelfReference_ShouldNotCauseStackOverflow()
        {
            // Arrange - SelfReferencing references itself
            var type = typeof(SelfReferencing);

            // Act & Assert - This should not throw a StackOverflowException
            var schema = _typeSchemaGenerator.GenerateSchema(type);

            // Verify the schema was generated
            Assert.NotNull(schema);
            Assert.Equal("object", schema.Type);
            Assert.NotNull(schema.Properties);
        }

        [Fact]
        public void GenerateSchema_WithDeepCircularReference_ShouldNotCauseStackOverflow()
        {
            // Arrange - A -> B -> C -> A circular chain
            var type = typeof(DeepCircularA);

            // Act & Assert - This should not throw a StackOverflowException
            var schema = _typeSchemaGenerator.GenerateSchema(type);

            // Verify the schema was generated
            Assert.NotNull(schema);
            Assert.Equal("object", schema.Type);
            Assert.NotNull(schema.Properties);
        }

        [Fact]
        public void GenerateSchema_WithCircularReference_ShouldCreatePlaceholderForCircularRef()
        {
            // Arrange
            var type = typeof(CircularA);

            // Act
            var schema = _typeSchemaGenerator.GenerateSchema(type);

            // Assert - Should have properties and one should be a circular reference placeholder
            Assert.NotNull(schema.Properties);
            Assert.True(schema.Properties.ContainsKey("B"));
            
            // The circular reference should be handled with a placeholder
            var circularProperty = schema.Properties["B"];
            Assert.NotNull(circularProperty);
            Assert.Equal("object", circularProperty.Type);
        }

        [Fact]
        public void GenerateSchema_CacheWorksCorrectlyAfterCircularReferenceResolution()
        {
            // Arrange
            var type = typeof(CircularA);

            // Act - Generate schema twice
            var schema1 = _typeSchemaGenerator.GenerateSchema(type);
            var schema2 = _typeSchemaGenerator.GenerateSchema(type);

            // Assert - Should return the same cached instance
            Assert.Same(schema1, schema2);
        }
    }

    // Test classes for circular reference testing
    public class CircularA
    {
        public string Name { get; set; } = string.Empty;
        public CircularB? B { get; set; }
    }

    public class CircularB
    {
        public string Description { get; set; } = string.Empty;
        public CircularA? A { get; set; }
    }

    public class SelfReferencing
    {
        public string Value { get; set; } = string.Empty;
        public SelfReferencing? Child { get; set; }
        public List<SelfReferencing>? Children { get; set; }
    }

    public class DeepCircularA
    {
        public string Name { get; set; } = string.Empty;
        public DeepCircularB? B { get; set; }
    }

    public class DeepCircularB
    {
        public string Description { get; set; } = string.Empty;
        public DeepCircularC? C { get; set; }
    }

    public class DeepCircularC
    {
        public string Value { get; set; } = string.Empty;
        public DeepCircularA? A { get; set; }
    }
}