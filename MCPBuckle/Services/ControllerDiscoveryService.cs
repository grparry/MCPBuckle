using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MCPBuckle.Attributes;
using MCPBuckle.Configuration;
using MCPBuckle.Models;

namespace MCPBuckle.Services
{
    /// <summary>
    /// Service for discovering controllers and their actions in an ASP.NET Core application.
    /// </summary>
    public class ControllerDiscoveryService : IControllerDiscoveryService
    {
        private readonly IActionDescriptorCollectionProvider _actionDescriptorCollectionProvider;
        private readonly XmlDocumentationService _xmlDocumentationService;
        private readonly TypeSchemaGenerator _typeSchemaGenerator;
        private readonly McpBuckleOptions _options;
        private readonly ILogger<ControllerDiscoveryService>? _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ControllerDiscoveryService"/> class.
        /// </summary>
        /// <param name="actionDescriptorCollectionProvider">The action descriptor collection provider.</param>
        /// <param name="xmlDocumentationService">The XML documentation service.</param>
        /// <param name="typeSchemaGenerator">The type schema generator.</param>
        /// <param name="options">The MCPBuckle options.</param>
        /// <param name="logger">The logger for logging messages.</param>
        public ControllerDiscoveryService(
            IActionDescriptorCollectionProvider actionDescriptorCollectionProvider,
            XmlDocumentationService xmlDocumentationService,
            TypeSchemaGenerator typeSchemaGenerator,
            IOptions<McpBuckleOptions> options,
            ILogger<ControllerDiscoveryService>? logger = null)
        {
            _actionDescriptorCollectionProvider = actionDescriptorCollectionProvider;
            _xmlDocumentationService = xmlDocumentationService;
            _typeSchemaGenerator = typeSchemaGenerator;
            _options = options.Value;
            _logger = logger;
        }

        /// <summary>
        /// Discovers all controllers and their actions in the application and converts them to MCP tools.
        /// </summary>
        /// <returns>A list of MCP tools representing the API endpoints.</returns>
        public List<McpTool> DiscoverTools()
        {
            var tools = new List<McpTool>();
            
            // Get action descriptors from ASP.NET Core infrastructure
            var actionDescriptors = _actionDescriptorCollectionProvider.ActionDescriptors.Items;
            _logger?.LogInformation("Discovering MCP tools from {Count} action descriptors", actionDescriptors.Count);
            
            // Track stats for logging
            int includedTools = 0;
            int excludedControllers = 0;
            int excludedMethods = 0;

            foreach (var descriptor in actionDescriptors)
            {
                if (descriptor is ControllerActionDescriptor controllerActionDescriptor)
                {
                    // Skip if controller is excluded or not included
                    if (ShouldSkipController(controllerActionDescriptor.ControllerName))
                    {
                        excludedControllers++;
                        continue;
                    }

                    var tool = CreateToolFromAction(controllerActionDescriptor);
                    if (tool != null)
                    {
                        tools.Add(tool);
                        includedTools++;
                    }
                    else
                    {
                        excludedMethods++;
                    }
                }
            }
            
            _logger?.LogInformation("MCP tool discovery complete. Included: {IncludedTools}, Excluded controllers: {ExcludedControllers}, Excluded methods: {ExcludedMethods}", 
                includedTools, excludedControllers, excludedMethods);

            return tools;
        }

        private bool ShouldSkipController(string controllerName)
        {
            // Skip if controller is in the exclude list
            if (_options.ExcludeControllers != null && _options.ExcludeControllers.Contains(controllerName))
            {
                return true;
            }

            // Skip if include list is specified and controller is not in it
            if (_options.IncludeControllers != null && _options.IncludeControllers.Count > 0)
            {
                return !_options.IncludeControllers.Contains(controllerName);
            }

            return false;
        }

        private McpTool? CreateToolFromAction(ControllerActionDescriptor actionDescriptor)
        {
            // Check for MCPExclude attribute on the controller level
            var controllerType = actionDescriptor.ControllerTypeInfo;
            var controllerExcludeAttr = controllerType.GetCustomAttribute<MCPExcludeAttribute>();
            if (controllerExcludeAttr != null)
            {
                _logger?.LogDebug("Excluding controller {ControllerName} due to MCPExclude attribute. Reason: {Reason}",
                    actionDescriptor.ControllerName,
                    string.IsNullOrEmpty(controllerExcludeAttr.Reason) ? "Not specified" : controllerExcludeAttr.Reason);
                return null;
            }

            // Check for MCPExclude attribute on the method level
            var methodInfo = actionDescriptor.MethodInfo;
            var methodExcludeAttr = methodInfo.GetCustomAttribute<MCPExcludeAttribute>();
            if (methodExcludeAttr != null)
            {
                _logger?.LogDebug("Excluding method {ControllerName}.{MethodName} due to MCPExclude attribute. Reason: {Reason}",
                    actionDescriptor.ControllerName,
                    actionDescriptor.ActionName,
                    string.IsNullOrEmpty(methodExcludeAttr.Reason) ? "Not specified" : methodExcludeAttr.Reason);
                return null;
            }

            // Skip actions that don't have an HTTP method attribute
            var httpMethodAttribute = actionDescriptor.MethodInfo
                .GetCustomAttributes()
                .FirstOrDefault(a => a.GetType().Name.StartsWith("Http") && a.GetType().Name.EndsWith("Attribute"));

            if (httpMethodAttribute == null)
            {
                return null;
            }

            // Get the HTTP method
            string httpMethod = httpMethodAttribute.GetType().Name.Replace("Http", "").Replace("Attribute", "").ToLowerInvariant();

            // Get the route template
            string routeTemplate = actionDescriptor.AttributeRouteInfo?.Template ?? string.Empty;

            // Create a name for the tool based on configuration
            string toolName;
            if (_options.CustomToolNameFactory != null)
            {
                toolName = _options.CustomToolNameFactory(actionDescriptor.ControllerName, actionDescriptor.ActionName);
            }
            else if (_options.IncludeControllerNameInToolName)
            {
                toolName = $"{actionDescriptor.ControllerName}_{actionDescriptor.ActionName}";
            }
            else
            {
                toolName = actionDescriptor.ActionName;
            }

            // Get documentation from XML comments
            string? description = null;
            if (actionDescriptor.MethodInfo != null && actionDescriptor.ControllerTypeInfo != null)
            {
                description = _xmlDocumentationService.GetMethodDocumentation(
                    actionDescriptor.ControllerTypeInfo,
                    actionDescriptor.MethodInfo);
            }

            // Create the MCP tool
            var tool = new McpTool
            {
                Name = toolName,
                Description = description ?? $"{httpMethod} {routeTemplate}",
                InputSchema = CreateInputSchema(actionDescriptor),
                OutputSchema = CreateOutputSchema(actionDescriptor)
            };

            // Populate annotations with handler and method info
            if (actionDescriptor.ControllerTypeInfo?.AsType() != null)
            {
                string? assemblyQualifiedName = actionDescriptor.ControllerTypeInfo.AsType().AssemblyQualifiedName;
                if (assemblyQualifiedName != null)
                {
                    tool.Annotations["HandlerTypeAssemblyQualifiedName"] = assemblyQualifiedName;
                }
            }
            if (actionDescriptor.MethodInfo != null)
            {
                tool.Annotations["MethodName"] = actionDescriptor.MethodInfo.Name;
            }

            return tool;
        }

        private McpSchema CreateInputSchema(ControllerActionDescriptor actionDescriptor)
        {
            var properties = new Dictionary<string, McpSchema>();
            var required = new List<string>();

            try
            {
                // 1. First extract route parameters from the route template (MCPInvoke 1.4.0+ compatibility)
                var routeParams = ExtractRouteParameters(actionDescriptor);
                foreach (var routeParam in routeParams)
                {
                    var routeSchema = new McpSchema
                    {
                        Type = MapDotNetTypeToJsonSchemaType(routeParam.Value),
                        Description = $"Route parameter {routeParam.Key}",
                        Source = "route",
                        IsRequired = true,
                        Annotations = new Dictionary<string, object>
                        {
                            ["source"] = "route"
                        }
                    };
                    
                    properties[routeParam.Key] = routeSchema;
                    required.Add(routeParam.Key);
                }

                // 2. Process method parameters
                foreach (var parameter in actionDescriptor.Parameters)
                {
                    // Skip if already handled as route parameter
                    if (routeParams.ContainsKey(parameter.Name))
                        continue;

                    // Skip ASP.NET Core infrastructure types
                    if (IsAspNetCoreInfrastructureType(parameter.ParameterType))
                        continue;

                    McpSchema paramSchema;

                    if (IsComplexType(parameter.ParameterType))
                    {
                        // Generate detailed schema for complex objects (MCPInvoke 1.4.0+ compatibility)
                        paramSchema = GenerateComplexObjectSchema(parameter.ParameterType, parameter.Name);
                    }
                    else
                    {
                        // Handle primitive types, arrays, and enums
                        paramSchema = new McpSchema
                        {
                            Type = MapDotNetTypeToJsonSchemaType(parameter.ParameterType),
                            Description = GetParameterDescription(actionDescriptor, parameter),
                            IsRequired = true // Default to required, will be adjusted based on parameter info
                        };

                        // Handle array types
                        if (IsArrayType(parameter.ParameterType))
                        {
                            var elementType = GetElementType(parameter.ParameterType);
                            paramSchema.Items = new McpSchema
                            {
                                Type = MapDotNetTypeToJsonSchemaType(elementType),
                                Description = $"Array item of type {elementType.Name}"
                            };

                            // Also populate annotations for backward compatibility
                            paramSchema.Annotations = new Dictionary<string, object>
                            {
                                ["items"] = new Dictionary<string, object>
                                {
                                    ["type"] = MapDotNetTypeToJsonSchemaType(elementType)
                                }
                            };
                        }
                        // Handle enum types
                        else if (parameter.ParameterType.IsEnum)
                        {
                            var enumValues = System.Enum.GetNames(parameter.ParameterType).Cast<object>().ToList();
                            paramSchema.Enum = enumValues;
                            paramSchema.Annotations = new Dictionary<string, object>
                            {
                                ["enum"] = enumValues
                            };
                        }
                    }

                    // Detect parameter source (MCPInvoke 1.4.0+ compatibility)
                    var parameterSource = DetectParameterSource(parameter, actionDescriptor);
                    if (!string.IsNullOrEmpty(parameterSource))
                    {
                        paramSchema.Source = parameterSource;
                        paramSchema.Annotations ??= new Dictionary<string, object>();
                        paramSchema.Annotations["source"] = parameterSource;
                    }

                    properties[parameter.Name] = paramSchema;
                    if (paramSchema.IsRequired)
                    {
                        required.Add(parameter.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error generating input schema for action {ControllerName}.{ActionName}", 
                    actionDescriptor.ControllerName, actionDescriptor.ActionName);
                // Fall back to basic schema generation
                return CreateBasicInputSchema(actionDescriptor);
            }

            return new McpSchema
            {
                Type = "object",
                Properties = properties,
                Required = required
            };
        }

        private McpSchema CreateOutputSchema(ControllerActionDescriptor actionDescriptor)
        {
            var methodInfo = actionDescriptor.MethodInfo;
            if (methodInfo == null)
            {
                // If we can't get the method info, return a generic schema
                return new McpSchema { Type = "object" };
            }

            // Get the return type of the method
            var returnType = methodInfo.ReturnType;

            // Handle async methods (Task<T>)
            if (returnType.IsGenericType && (returnType.GetGenericTypeDefinition() == typeof(Task<>) ||
                                            returnType.Name.StartsWith("ValueTask`")))
            {
                returnType = returnType.GetGenericArguments()[0];
            }
            // Handle non-generic Task which returns void
            else if (returnType == typeof(Task) || returnType.Name == "ValueTask")
            {
                return new McpSchema { Type = "null" };
            }

            // Handle common ASP.NET Core types
            if (returnType.IsGenericType && returnType.GetGenericTypeDefinition().Name.StartsWith("ActionResult`"))
            {
                // ActionResult<T>, extract T
                returnType = returnType.GetGenericArguments()[0];
            }
            else if (typeof(IActionResult).IsAssignableFrom(returnType))
            {
                // For generic IActionResult, we can't determine the exact type at compile time
                // We'll return a generic object schema
                return new McpSchema { Type = "object" };
            }
            else if (returnType == typeof(void))
            {
                return new McpSchema { Type = "null" };
            }

            // Check if the return type is a collection or array
            bool isCollection = returnType.IsArray || 
                               (returnType.IsGenericType && 
                                typeof(System.Collections.IEnumerable).IsAssignableFrom(returnType) && 
                                returnType != typeof(string));

            if (isCollection)
            {
                // For collections and arrays, create an array schema
                Type itemType;
                if (returnType.IsArray)
                {
                    itemType = returnType.GetElementType() ?? typeof(object);
                }
                else
                {
                    // Try to get the generic argument (T in IEnumerable<T>)
                    var genericArgs = returnType.GetGenericArguments();
                    itemType = genericArgs.Length > 0 ? genericArgs[0] : typeof(object);
                }

                return new McpSchema
                {
                    Type = "array",
                    Items = _typeSchemaGenerator.GenerateSchema(itemType)
                };
            }
            
            // For all other types, use the type schema generator
            return _typeSchemaGenerator.GenerateSchema(returnType);
        }

        #region MCPInvoke 1.4.0+ Compatibility Methods

        /// <summary>
        /// Extracts route parameters from the action descriptor's route template.
        /// </summary>
        private Dictionary<string, Type> ExtractRouteParameters(ControllerActionDescriptor actionDescriptor)
        {
            var routeParams = new Dictionary<string, Type>();
            var routeTemplate = actionDescriptor.AttributeRouteInfo?.Template;
            
            if (string.IsNullOrEmpty(routeTemplate))
                return routeParams;

            // Parse route template for parameters like {id}, {tenantId:int}, etc.
            var matches = System.Text.RegularExpressions.Regex.Matches(routeTemplate, @"\{([^}:]+)(?::[^}]+)?\}");
            
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                var paramName = match.Groups[1].Value;
                
                // Try to find corresponding method parameter to get actual type
                var methodParam = actionDescriptor.Parameters.FirstOrDefault(p => 
                    string.Equals(p.Name, paramName, StringComparison.OrdinalIgnoreCase));
                
                if (methodParam != null)
                {
                    routeParams[paramName] = methodParam.ParameterType;
                }
                else
                {
                    // Default to string if we can't determine the type
                    routeParams[paramName] = typeof(string);
                }
            }
            
            return routeParams;
        }

        /// <summary>
        /// Maps .NET types to JSON Schema types.
        /// </summary>
        private static string MapDotNetTypeToJsonSchemaType(Type type)
        {
            if (type == typeof(string)) return "string";
            if (type == typeof(int) || type == typeof(long) || type == typeof(short) || 
                type == typeof(uint) || type == typeof(ulong) || type == typeof(ushort)) return "integer";
            if (type == typeof(float) || type == typeof(double) || type == typeof(decimal)) return "number";
            if (type == typeof(bool)) return "boolean";
            if (type == typeof(DateTime) || type == typeof(DateTimeOffset)) return "string";
            if (type == typeof(Guid)) return "string";
            if (type.IsEnum) return "string";
            if (type.IsArray || typeof(System.Collections.IEnumerable).IsAssignableFrom(type)) return "array";
            if (type.IsClass || type.IsValueType) return "object";
            
            return "string"; // fallback
        }

        /// <summary>
        /// Detects the parameter source (route, body, query, header) for a given parameter.
        /// </summary>
        private static string DetectParameterSource(dynamic parameter, ControllerActionDescriptor actionDescriptor)
        {
            // Check binding source from parameter attributes
            if (parameter.BindingInfo?.BindingSource != null)
            {
                var source = parameter.BindingInfo.BindingSource.Id?.ToLowerInvariant();
                switch (source)
                {
                    case "body": return "body";
                    case "query": return "query";
                    case "header": return "header";
                    case "route": return "route";
                    case "path": return "route";
                }
            }

            // Additional [FromQuery] attribute detection for complex objects
            // This fixes the issue where complex objects with [FromQuery] were incorrectly classified as "body"
            try
            {
                // Get the corresponding method parameter to check for [FromQuery] attribute
                var methodInfo = actionDescriptor.MethodInfo;
                if (methodInfo != null)
                {
                    var methodParam = methodInfo.GetParameters()
                        .FirstOrDefault(p => p.Name == parameter.Name);
                    
                    if (methodParam?.GetCustomAttribute<FromQueryAttribute>() != null)
                    {
                        return "query";
                    }
                }
            }
            catch
            {
                // Ignore reflection errors and continue with existing logic
            }

            // Check if it's a route parameter by looking at the route template
            var routeTemplate = actionDescriptor.AttributeRouteInfo?.Template ?? string.Empty;
            if (routeTemplate.Contains($"{{{parameter.Name}}}") || 
                routeTemplate.Contains($"{{{parameter.Name}:"))
            {
                return "route";
            }

            // Complex types typically come from body (unless explicitly marked with [FromQuery] above)
            if (IsComplexType(parameter.ParameterType))
            {
                return "body";
            }

            // Primitive types typically come from query
            return "query";
        }

        /// <summary>
        /// Determines if a type is a complex object type.
        /// </summary>
        private static bool IsComplexType(Type type)
        {
            return type.IsClass && 
                   type != typeof(string) && 
                   type != typeof(object) && 
                   !type.IsPrimitive && 
                   !type.IsEnum &&
                   !typeof(System.Collections.IEnumerable).IsAssignableFrom(type);
        }

        /// <summary>
        /// Determines if a type is an array type.
        /// </summary>
        private static bool IsArrayType(Type type)
        {
            return type.IsArray || 
                   (type.IsGenericType && typeof(System.Collections.IEnumerable).IsAssignableFrom(type) && type != typeof(string));
        }

        /// <summary>
        /// Gets the element type of an array or collection.
        /// </summary>
        private static Type GetElementType(Type type)
        {
            if (type.IsArray)
                return type.GetElementType() ?? typeof(object);
            
            if (type.IsGenericType)
            {
                var args = type.GetGenericArguments();
                return args.Length > 0 ? args[0] : typeof(object);
            }
            
            return typeof(object);
        }

        /// <summary>
        /// Determines if a type is an ASP.NET Core infrastructure type that should be skipped.
        /// </summary>
        private static bool IsAspNetCoreInfrastructureType(Type type)
        {
            return type == typeof(HttpContext) ||
                   type == typeof(HttpRequest) ||
                   type == typeof(HttpResponse) ||
                   type == typeof(System.Threading.CancellationToken) ||
                   type.Namespace?.StartsWith("Microsoft.AspNetCore") == true;
        }

        /// <summary>
        /// Generates a complex object schema with detailed property information.
        /// </summary>
        private McpSchema GenerateComplexObjectSchema(Type objectType, string paramName)
        {
            var objectProperties = GenerateObjectProperties(objectType);
            var requiredProps = GetRequiredProperties(objectType);
            
            var schema = new McpSchema
            {
                Type = "object",
                Description = $"Complex object of type {objectType.Name}",
                IsRequired = true,
                Properties = ConvertObjectPropertiesToMcpSchemas(objectProperties),
                Required = requiredProps,
                Annotations = new Dictionary<string, object>
                {
                    ["properties"] = objectProperties,
                    ["required"] = requiredProps
                }
            };
            
            return schema;
        }

        /// <summary>
        /// Generates properties for a complex object type.
        /// </summary>
        private Dictionary<string, object> GenerateObjectProperties(Type objectType)
        {
            return GenerateObjectProperties(objectType, new HashSet<Type>());
        }

        private Dictionary<string, object> GenerateObjectProperties(Type objectType, HashSet<Type> processedTypes)
        {
            var properties = new Dictionary<string, object>();
            
            // Prevent circular references
            if (processedTypes.Contains(objectType))
            {
                return new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["description"] = $"Circular reference to {objectType.Name}"
                };
            }

            processedTypes.Add(objectType);
            
            // Walk the full inheritance chain to include base class properties
            // This fixes the issue where inherited properties (like Provider, ModelName, PromptVersion) were missing
            var objectProperties = GetInheritanceChainProperties(objectType);
            
            foreach (var prop in objectProperties)
            {
                var propType = MapDotNetTypeToJsonSchemaType(prop.PropertyType);
                var propInfo = new Dictionary<string, object>
                {
                    ["type"] = propType,
                    ["description"] = prop.Name
                };

                // Handle nested objects
                if (IsComplexType(prop.PropertyType))
                {
                    propInfo["properties"] = GenerateObjectProperties(prop.PropertyType, new HashSet<Type>(processedTypes));
                }
                // Handle arrays
                else if (IsArrayType(prop.PropertyType))
                {
                    var elementType = GetElementType(prop.PropertyType);
                    var itemInfo = new Dictionary<string, object>
                    {
                        ["type"] = MapDotNetTypeToJsonSchemaType(elementType)
                    };
                    
                    // Handle complex element types
                    if (IsComplexType(elementType))
                    {
                        itemInfo["properties"] = GenerateObjectProperties(elementType, new HashSet<Type>(processedTypes));
                    }
                    
                    propInfo["items"] = itemInfo;
                }
                // Handle enums
                else if (prop.PropertyType.IsEnum)
                {
                    propInfo["enum"] = System.Enum.GetNames(prop.PropertyType);
                }

                properties[prop.Name] = propInfo;
            }
            
            return properties;
        }

        /// <summary>
        /// Gets the required properties for a complex object type.
        /// </summary>
        private List<string> GetRequiredProperties(Type objectType)
        {
            var required = new List<string>();
            var properties = objectType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            
            foreach (var prop in properties)
            {
                // Check for Required attribute
                if (prop.GetCustomAttribute<System.ComponentModel.DataAnnotations.RequiredAttribute>() != null)
                {
                    required.Add(prop.Name);
                }
                // Non-nullable value types are typically required
                else if (prop.PropertyType.IsValueType && Nullable.GetUnderlyingType(prop.PropertyType) == null)
                {
                    required.Add(prop.Name);
                }
            }
            
            return required;
        }

        /// <summary>
        /// Converts object properties dictionary to McpSchema dictionary.
        /// </summary>
        private Dictionary<string, McpSchema> ConvertObjectPropertiesToMcpSchemas(Dictionary<string, object> objectProperties)
        {
            var mcpProperties = new Dictionary<string, McpSchema>();
            
            foreach (var prop in objectProperties)
            {
                if (prop.Value is Dictionary<string, object> propDef)
                {
                    var mcpParam = new McpSchema
                    {
                        Type = propDef.TryGetValue("type", out var typeObj) ? typeObj?.ToString() ?? "string" : "string",
                        Description = propDef.TryGetValue("description", out var descObj) ? descObj?.ToString() ?? "" : ""
                    };
                    
                    // Handle nested object properties
                    if (mcpParam.Type == "object" && propDef.TryGetValue("properties", out var nestedPropsObj) 
                        && nestedPropsObj is Dictionary<string, object> nestedProps)
                    {
                        mcpParam.Properties = ConvertObjectPropertiesToMcpSchemas(nestedProps);
                    }
                    
                    // Handle array items
                    if (mcpParam.Type == "array" && propDef.TryGetValue("items", out var itemsObj))
                    {
                        if (itemsObj is Dictionary<string, object> itemsDef)
                        {
                            mcpParam.Items = new McpSchema
                            {
                                Type = itemsDef.TryGetValue("type", out var itemTypeObj) ? itemTypeObj?.ToString() ?? "string" : "string"
                            };
                        }
                    }
                    
                    // Handle enum values
                    if (propDef.TryGetValue("enum", out var enumObj) && enumObj is string[] enumValues)
                    {
                        mcpParam.Enum = enumValues.Cast<object>().ToList();
                    }
                    
                    mcpProperties[prop.Key] = mcpParam;
                }
            }
            
            return mcpProperties;
        }

        /// <summary>
        /// Gets parameter description from XML documentation or attributes.
        /// </summary>
        private string GetParameterDescription(ControllerActionDescriptor actionDescriptor, dynamic parameter)
        {
            // Try XML documentation first
            if (_options.IncludeXmlDocumentation)
            {
                var xmlDesc = _xmlDocumentationService.GetParameterDocumentation(
                    actionDescriptor.ControllerTypeInfo,
                    actionDescriptor.MethodInfo,
                    parameter.Name);
                
                if (!string.IsNullOrEmpty(xmlDesc))
                    return xmlDesc;
            }

            // Try to get parameter info if available
            try
            {
                var paramInfo = actionDescriptor.MethodInfo?.GetParameters().FirstOrDefault(p => p.Name == parameter.Name);
                if (paramInfo != null)
                {
                    // Try DisplayName attribute
                    var displayNameAttr = paramInfo.GetCustomAttribute<System.ComponentModel.DisplayNameAttribute>();
                    if (displayNameAttr != null)
                        return displayNameAttr.DisplayName;

                    // Try Description attribute  
                    var descriptionAttr = paramInfo.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>();
                    if (descriptionAttr != null)
                        return descriptionAttr.Description;
                }
            }
            catch
            {
                // Ignore errors getting parameter info
            }

            // Default description
            return $"Parameter of type {parameter.ParameterType.Name}";
        }

        /// <summary>
        /// Creates a basic input schema as fallback when enhanced schema generation fails.
        /// </summary>
        private McpSchema CreateBasicInputSchema(ControllerActionDescriptor actionDescriptor)
        {
            var properties = new Dictionary<string, McpSchema>();
            var required = new List<string>();

            foreach (var parameter in actionDescriptor.Parameters)
            {
                var paramSchema = _typeSchemaGenerator.GenerateSchema(parameter.ParameterType);
                properties[parameter.Name] = paramSchema;
                
                // Mark as required by default - more sophisticated logic could be added later
                // if (!parameter.IsOptional)
                {
                    required.Add(parameter.Name);
                }
            }

            return new McpSchema
            {
                Type = "object",
                Properties = properties,
                Required = required
            };
        }

        /// <summary>
        /// Gets properties from the entire inheritance chain of a type.
        /// This ensures that base class properties are included in schema generation.
        /// </summary>
        private PropertyInfo[] GetInheritanceChainProperties(Type type)
        {
            var properties = new List<PropertyInfo>();
            var currentType = type;

            // Walk up inheritance chain - essential for PromptRequest : LlmProviderModelRequest
            while (currentType != null && currentType != typeof(object))
            {
                var declaredProperties = currentType.GetProperties(
                    BindingFlags.Public | 
                    BindingFlags.Instance | 
                    BindingFlags.DeclaredOnly)
                    .Where(p => p.CanRead);
                
                properties.AddRange(declaredProperties);
                currentType = currentType.BaseType;
            }

            return properties.ToArray();
        }

        #endregion
    }
}
