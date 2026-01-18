using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using MCPBuckle.Configuration;
using MCPBuckle.Models;
using Microsoft.Extensions.Options;

namespace MCPBuckle.Services
{
    /// <summary>
    /// Service for generating MCP schemas from .NET types.
    /// </summary>
    public class TypeSchemaGenerator
    {
        private readonly XmlDocumentationService _xmlDocumentationService;
        private readonly McpBuckleOptions _options;
        private readonly Dictionary<Type, McpSchema> _schemaCache = new Dictionary<Type, McpSchema>();
        private readonly HashSet<Type> _typesBeingProcessed = new HashSet<Type>();

        /// <summary>
        /// Initializes a new instance of the <see cref="TypeSchemaGenerator"/> class.
        /// </summary>
        /// <param name="xmlDocumentationService">The XML documentation service.</param>
        /// <param name="options">The MCPBuckle options.</param>
        public TypeSchemaGenerator(
            XmlDocumentationService xmlDocumentationService,
            IOptions<McpBuckleOptions> options)
        {
            _xmlDocumentationService = xmlDocumentationService;
            _options = options.Value;
        }

        /// <summary>
        /// Generates an MCP schema for the specified type.
        /// </summary>
        /// <param name="type">The type to generate a schema for.</param>
        /// <returns>The generated MCP schema.</returns>
        public virtual McpSchema GenerateSchema(Type type)
        {
            // Return from cache if available
            if (_schemaCache.TryGetValue(type, out var cachedSchema))
            {
                return cachedSchema;
            }

            // Check for circular reference
            if (_typesBeingProcessed.Contains(type))
            {
                // Create a placeholder schema for circular references
                var circularRefSchema = new McpSchema
                {
                    Type = "object",
                    Description = $"Circular reference to {type.Name}",
                    Properties = new Dictionary<string, McpSchema>(),
                    Required = new List<string>()
                };
                
                // Cache the placeholder immediately to break the cycle
                _schemaCache[type] = circularRefSchema;
                return circularRefSchema;
            }

            // Mark this type as being processed
            _typesBeingProcessed.Add(type);

            try
            {
#if NET7_0_OR_GREATER
                // Check for polymorphic types first (JsonPolymorphic attribute)
                var polymorphicInfo = GetPolymorphicTypeInfo(type);
                if (polymorphicInfo != null)
                {
                    return GeneratePolymorphicSchema(polymorphicInfo);
                }
#endif

                // Special handling for enum types
                if (type.IsEnum)
                {
                    return GenerateEnumSchema(type);
                }

                // Handle nullable types
                if (Nullable.GetUnderlyingType(type) is Type underlyingType)
                {
                    return GenerateSchema(underlyingType);
                }

            // Generate schema based on type
            var schema = new McpSchema
            {
                Type = GetJsonSchemaType(type)
            };

            // Handle different types
            if (type == typeof(string))
            {
                // Handle string format (email, uri, etc.)
                var formatAttribute = type.GetCustomAttribute<DisplayFormatAttribute>();
                if (formatAttribute != null && !string.IsNullOrEmpty(formatAttribute.DataFormatString))
                {
                    schema.Format = formatAttribute.DataFormatString;
                }
            }
            // This code is unreachable - enums are handled above with return statement
            // else if (type.IsEnum)
            // {
            //     // Handle enums
            //     schema.Type = "string";
            //     schema.AdditionalProperties["enum"] = Enum.GetNames(type);
            // }
            else if (IsDictionaryType(type))
            {
                // Handle dictionaries - must check before IsArrayType since Dictionary implements IEnumerable
                schema.Type = "object";
                schema.AdditionalProperties["additionalProperties"] = true;
            }
            else if (IsArrayType(type))
            {
                // Handle arrays and collections
                schema.Type = "array";
                var elementType = GetElementType(type);
                schema.Items = GenerateSchema(elementType);
            }
            else if (type.IsClass && type != typeof(string))
            {
                // Handle complex types (classes)
                schema.Type = "object";
                schema.Properties = new Dictionary<string, McpSchema>();

                // Get properties
                var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.CanRead && !p.GetCustomAttributes<IgnoreDataMemberAttribute>().Any());

                foreach (var property in properties)
                {
                    var propertySchema = GenerateSchema(property.PropertyType);
                    
                    // Add property description from XML documentation if available
                    if (_options.IncludePropertyDescriptions)
                    {
                        var propertyDescription = GetPropertyDescription(type, property);
                        if (!string.IsNullOrEmpty(propertyDescription))
                        {
                            propertySchema.Description = propertyDescription;
                        }
                    }

                    // Check if property is required
                    var isRequired = property.GetCustomAttribute<RequiredAttribute>() != null;
                    if (isRequired)
                    {
                        if (schema.Required == null)
                        {
                            schema.Required = new List<string>();
                        }
                        schema.Required.Add(property.Name);
                    }

                    // Add property to schema
                    schema.Properties[property.Name] = propertySchema;
                }
            }

                // Cache the schema
                _schemaCache[type] = schema;

                return schema;
            }
            finally
            {
                // Remove from processing set when done
                _typesBeingProcessed.Remove(type);
            }
        }

        private string GetJsonSchemaType(Type type)
        {
            if (type == typeof(string) || type == typeof(char) || type == typeof(Guid))
                return "string";
            else if (type == typeof(bool))
                return "boolean";
            else if (type == typeof(byte) || type == typeof(sbyte) || 
                     type == typeof(short) || type == typeof(ushort) ||
                     type == typeof(int) || type == typeof(uint) ||
                     type == typeof(long) || type == typeof(ulong))
                return "integer";
            else if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
                return "number";
            else if (type == typeof(DateTime) || type == typeof(DateTimeOffset))
                return "string"; // With format: date-time
            else if (type == typeof(DateOnly))
                return "string"; // With format: date
            else if (type == typeof(TimeOnly))
                return "string"; // With format: time
            else if (IsArrayType(type))
                return "array";
            else
                return "object";
        }

        private bool IsArrayType(Type type)
        {
            return type.IsArray || 
                   (type.IsGenericType && typeof(IEnumerable).IsAssignableFrom(type));
        }

        private bool IsDictionaryType(Type type)
        {
            return type.IsGenericType && 
                   (type.GetGenericTypeDefinition() == typeof(Dictionary<,>) ||
                    type.GetGenericTypeDefinition() == typeof(IDictionary<,>));
        }

        private Type GetElementType(Type type)
        {
            if (type.IsArray)
            {
                return type.GetElementType() ?? typeof(object);
            }
            else if (type.IsGenericType && typeof(IEnumerable).IsAssignableFrom(type))
            {
                return type.GetGenericArguments().FirstOrDefault() ?? typeof(object);
            }
            
            return typeof(object);
        }

        /// <summary>
        /// Generate schema specifically for enum types, with special handling for JsonStringEnumConverter.
        /// </summary>
        /// <param name="enumType">The enum type to generate schema for</param>
        /// <returns>An MCP schema for the enum type</returns>
        private McpSchema GenerateEnumSchema(Type enumType)
        {
            // Check if this enum uses JsonStringEnumConverter
            bool usesStringEnum = enumType.GetCustomAttributes(true)
                .Any(attr => attr.GetType().Name.Contains("JsonStringEnumConverter"));
                
            // Create schema
            var schema = new McpSchema
            {
                // Enums with JsonStringEnumConverter are strings, otherwise we treat them as integers
                Type = usesStringEnum ? "string" : "integer",
                Properties = new Dictionary<string, McpSchema>(),
                Required = new List<string>()
            };
            
            // Add enum values to schema
            if (usesStringEnum)
            {
                var enumValues = Enum.GetNames(enumType);
                schema.Enum = enumValues.Cast<object>().ToList();
            }
            else
            {
                var enumValues = Enum.GetValues(enumType);
                schema.Enum = enumValues.Cast<int>().Cast<object>().ToList();
            }
            
            // Get enum description if available
            var descriptionAttribute = enumType.GetCustomAttribute<DescriptionAttribute>();
            if (descriptionAttribute != null)
            {
                schema.Description = descriptionAttribute.Description;
            }
            
            // Cache the schema (enum schemas don't have circular reference issues)
            _schemaCache[enumType] = schema;
            
            return schema;
        }
        
        private string? GetPropertyDescription(Type type, PropertyInfo property)
        {
            // Try to get description from XML documentation
            if (_options.IncludeXmlDocumentation)
            {
                var typeInfo = type.GetTypeInfo();
                var propertyName = property.Name;
                
                // TODO: Implement XML documentation for properties
                // This would require enhancing the XmlDocumentationService
                
                // For now, we'll use the DisplayName or Description attribute as a fallback
                var displayAttribute = property.GetCustomAttribute<DisplayNameAttribute>();
                if (displayAttribute != null)
                {
                    return displayAttribute.DisplayName;
                }
                
                var descriptionAttribute = property.GetCustomAttribute<DescriptionAttribute>();
                if (descriptionAttribute != null)
                {
                    return descriptionAttribute.Description;
                }
            }
            
            return null;
        }

        /// <summary>
        /// Attribute to indicate that a property should be ignored in the schema.
        /// </summary>
        [AttributeUsage(AttributeTargets.Property)]
        public class IgnoreDataMemberAttribute : Attribute
        {
        }

#if NET7_0_OR_GREATER
        /// <summary>
        /// Detects if a type is a polymorphic base type with JsonPolymorphic and JsonDerivedType attributes.
        /// </summary>
        /// <param name="type">The type to check.</param>
        /// <returns>Polymorphic type info if the type is polymorphic, null otherwise.</returns>
        public PolymorphicTypeInfo? GetPolymorphicTypeInfo(Type type)
        {
            // Look for JsonPolymorphicAttribute
            var polymorphicAttr = type.GetCustomAttribute<JsonPolymorphicAttribute>();
            if (polymorphicAttr == null)
            {
                return null;
            }

            var info = new PolymorphicTypeInfo
            {
                BaseType = type,
                DiscriminatorPropertyName = polymorphicAttr.TypeDiscriminatorPropertyName ?? "$type"
            };

            // Look for JsonDerivedTypeAttribute on the type
            var derivedTypeAttrs = type.GetCustomAttributes<JsonDerivedTypeAttribute>();
            foreach (var attr in derivedTypeAttrs)
            {
                var derivedType = attr.DerivedType;
                string discriminatorValue;

                // Get the discriminator value - can be string or int
                if (attr.TypeDiscriminator is string strDiscriminator)
                {
                    discriminatorValue = strDiscriminator;
                }
                else if (attr.TypeDiscriminator is int intDiscriminator)
                {
                    discriminatorValue = intDiscriminator.ToString();
                }
                else if (attr.TypeDiscriminator != null)
                {
                    discriminatorValue = attr.TypeDiscriminator.ToString() ?? derivedType.Name;
                }
                else
                {
                    // Fall back to type name if no discriminator specified
                    discriminatorValue = derivedType.Name;
                    info.UseTypeNameAsDiscriminator = true;
                }

                info.DerivedTypes[discriminatorValue] = derivedType;
            }

            return info.DerivedTypes.Count > 0 ? info : null;
        }

        /// <summary>
        /// Generates a polymorphic schema with oneOf and discriminator.
        /// </summary>
        /// <param name="polymorphicInfo">The polymorphic type information.</param>
        /// <returns>The generated polymorphic schema.</returns>
        public McpSchema GeneratePolymorphicSchema(PolymorphicTypeInfo polymorphicInfo)
        {
            var schema = new McpSchema
            {
                OneOf = new List<McpSchema>(),
                Discriminator = new McpDiscriminator
                {
                    PropertyName = polymorphicInfo.DiscriminatorPropertyName,
                    Mapping = new Dictionary<string, string>()
                },
                Definitions = new Dictionary<string, McpSchema>(),
                SourceType = polymorphicInfo.BaseType,
                IsPolymorphicBase = true
            };

            foreach (var (discriminatorValue, derivedType) in polymorphicInfo.DerivedTypes)
            {
                var typeName = derivedType.Name;
                var refPath = $"#/$defs/{typeName}";

                // Add to discriminator mapping
                schema.Discriminator.Mapping[discriminatorValue] = refPath;

                // Add ref to oneOf
                schema.OneOf.Add(new McpSchema
                {
                    Ref = refPath,
                    SourceType = derivedType,
                    DiscriminatorValue = discriminatorValue
                });

                // Generate the derived type schema with const discriminator
                var derivedSchema = GenerateDerivedTypeSchema(
                    derivedType,
                    polymorphicInfo.DiscriminatorPropertyName,
                    discriminatorValue);

                schema.Definitions[typeName] = derivedSchema;
            }

            // Cache the polymorphic schema
            _schemaCache[polymorphicInfo.BaseType] = schema;

            return schema;
        }

        /// <summary>
        /// Generates a schema for a derived type in a polymorphic hierarchy, including const discriminator.
        /// </summary>
        /// <param name="derivedType">The derived type.</param>
        /// <param name="discriminatorPropertyName">The name of the discriminator property.</param>
        /// <param name="discriminatorValue">The discriminator value for this type.</param>
        /// <returns>The generated schema for the derived type.</returns>
        public McpSchema GenerateDerivedTypeSchema(Type derivedType, string discriminatorPropertyName, string discriminatorValue)
        {
            var schema = new McpSchema
            {
                Type = "object",
                Properties = new Dictionary<string, McpSchema>(),
                Required = new List<string>(),
                SourceType = derivedType,
                DiscriminatorValue = discriminatorValue
            };

            // Add the discriminator property with const value
            schema.Properties[discriminatorPropertyName] = new McpSchema
            {
                Type = "string",
                Const = discriminatorValue
            };
            schema.Required.Add(discriminatorPropertyName);

            // Get properties (excluding the discriminator if it exists as a real property)
            var properties = derivedType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead &&
                            !p.GetCustomAttributes<IgnoreDataMemberAttribute>().Any() &&
                            !p.GetCustomAttributes<JsonIgnoreAttribute>().Any() &&
                            !string.Equals(p.Name, discriminatorPropertyName, StringComparison.OrdinalIgnoreCase));

            foreach (var property in properties)
            {
                var propertySchema = GenerateSchema(property.PropertyType);

                // Add property description from attributes
                if (_options.IncludePropertyDescriptions)
                {
                    var propertyDescription = GetPropertyDescription(derivedType, property);
                    if (!string.IsNullOrEmpty(propertyDescription))
                    {
                        propertySchema.Description = propertyDescription;
                    }
                }

                // Check if property is required
                var isRequired = property.GetCustomAttribute<RequiredAttribute>() != null;
                if (isRequired)
                {
                    schema.Required.Add(property.Name);
                }

                // Use JsonPropertyName if present
                var jsonPropertyName = property.GetCustomAttribute<JsonPropertyNameAttribute>();
                var propName = jsonPropertyName?.Name ?? property.Name;

                schema.Properties[propName] = propertySchema;
            }

            return schema;
        }
#endif
    }
}
