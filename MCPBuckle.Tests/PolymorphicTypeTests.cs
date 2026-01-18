using System.Collections.Generic;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Xunit;
using MCPBuckle.Configuration;
using MCPBuckle.Models;
using MCPBuckle.Services;

namespace MCPBuckle.Tests
{
#if NET7_0_OR_GREATER
    /// <summary>
    /// Tests for polymorphic type support using System.Text.Json attributes.
    /// These tests require .NET 7.0+ for JsonPolymorphic/JsonDerivedType attributes.
    /// </summary>
    public class PolymorphicTypeTests
    {
        private readonly TypeSchemaGenerator _typeSchemaGenerator;

        public PolymorphicTypeTests()
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
        public void GenerateSchema_ForPolymorphicType_CreatesOneOfSchema()
        {
            // Act
            var schema = _typeSchemaGenerator.GenerateSchema(typeof(TestShape));

            // Assert
            Assert.NotNull(schema);
            Assert.NotNull(schema.OneOf);
            Assert.Equal(2, schema.OneOf.Count);
            Assert.True(schema.IsPolymorphicBase);
        }

        [Fact]
        public void GenerateSchema_ForPolymorphicType_CreatesDiscriminator()
        {
            // Act
            var schema = _typeSchemaGenerator.GenerateSchema(typeof(TestShape));

            // Assert
            Assert.NotNull(schema);
            Assert.NotNull(schema.Discriminator);
            Assert.Equal("shapeType", schema.Discriminator.PropertyName);
            Assert.NotNull(schema.Discriminator.Mapping);
            Assert.Equal(2, schema.Discriminator.Mapping.Count);
        }

        [Fact]
        public void GenerateSchema_ForPolymorphicType_CreatesDefinitions()
        {
            // Act
            var schema = _typeSchemaGenerator.GenerateSchema(typeof(TestShape));

            // Assert
            Assert.NotNull(schema);
            Assert.NotNull(schema.Definitions);
            Assert.True(schema.Definitions.ContainsKey("TestCircle"));
            Assert.True(schema.Definitions.ContainsKey("TestRectangle"));
        }

        [Fact]
        public void GenerateSchema_ForPolymorphicType_DerivedTypeHasConstDiscriminator()
        {
            // Act
            var schema = _typeSchemaGenerator.GenerateSchema(typeof(TestShape));

            // Assert
            Assert.NotNull(schema);
            Assert.NotNull(schema.Definitions);

            var circleSchema = schema.Definitions["TestCircle"];
            Assert.NotNull(circleSchema.Properties);
            Assert.True(circleSchema.Properties.ContainsKey("shapeType"));
            Assert.Equal("circle", circleSchema.Properties["shapeType"].Const);
        }

        [Fact]
        public void GenerateSchema_ForPolymorphicType_DerivedTypeHasOwnProperties()
        {
            // Act
            var schema = _typeSchemaGenerator.GenerateSchema(typeof(TestShape));

            // Assert
            Assert.NotNull(schema);
            Assert.NotNull(schema.Definitions);

            var circleSchema = schema.Definitions["TestCircle"];
            Assert.NotNull(circleSchema.Properties);
            Assert.True(circleSchema.Properties.ContainsKey("Radius"));
            Assert.Equal("number", circleSchema.Properties["Radius"].Type);

            var rectangleSchema = schema.Definitions["TestRectangle"];
            Assert.NotNull(rectangleSchema.Properties);
            Assert.True(rectangleSchema.Properties.ContainsKey("Width"));
            Assert.True(rectangleSchema.Properties.ContainsKey("Height"));
        }

        [Fact]
        public void GenerateSchema_ForPolymorphicType_DiscriminatorIsRequired()
        {
            // Act
            var schema = _typeSchemaGenerator.GenerateSchema(typeof(TestShape));

            // Assert
            Assert.NotNull(schema);
            Assert.NotNull(schema.Definitions);

            foreach (var (_, defSchema) in schema.Definitions)
            {
                Assert.NotNull(defSchema.Required);
                Assert.Contains("shapeType", defSchema.Required);
            }
        }

        [Fact]
        public void GenerateSchema_ForPolymorphicType_OneOfReferencesDefinitions()
        {
            // Act
            var schema = _typeSchemaGenerator.GenerateSchema(typeof(TestShape));

            // Assert
            Assert.NotNull(schema);
            Assert.NotNull(schema.OneOf);

            foreach (var variant in schema.OneOf)
            {
                Assert.NotNull(variant.Ref);
                Assert.StartsWith("#/$defs/", variant.Ref);
            }
        }

        [Fact]
        public void GenerateSchema_ForPolymorphicType_DiscriminatorMappingMatchesOneOf()
        {
            // Act
            var schema = _typeSchemaGenerator.GenerateSchema(typeof(TestShape));

            // Assert
            Assert.NotNull(schema);
            Assert.NotNull(schema.Discriminator?.Mapping);
            Assert.NotNull(schema.OneOf);

            // Each oneOf ref should have a corresponding mapping entry
            foreach (var variant in schema.OneOf)
            {
                Assert.Contains(schema.Discriminator.Mapping.Values, v => v == variant.Ref);
            }
        }
    }

    // Test types for polymorphic schema generation
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "shapeType")]
    [JsonDerivedType(typeof(TestCircle), "circle")]
    [JsonDerivedType(typeof(TestRectangle), "rectangle")]
    public abstract class TestShape
    {
        public string? Name { get; set; }
    }

    public class TestCircle : TestShape
    {
        public double Radius { get; set; }
    }

    public class TestRectangle : TestShape
    {
        public double Width { get; set; }
        public double Height { get; set; }
    }
#endif
}
