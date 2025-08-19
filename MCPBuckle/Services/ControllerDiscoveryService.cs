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
                // 1. ENHANCED: Extract route parameters with comprehensive metadata (MCPBuckle v2.0)
                var routeParams = ExtractRouteParameters(actionDescriptor);
                var routeInfo = AnalyzeRouteTemplates(actionDescriptor);
                var httpMethod = GetHttpMethodFromActionDescriptor(actionDescriptor);
                
                foreach (var routeParam in routeParams)
                {
                    // ENHANCED: Check if this route parameter has explicit binding information
                    var matchingParam = actionDescriptor.Parameters
                        .FirstOrDefault(p => p.Name.Equals(routeParam.Key, StringComparison.OrdinalIgnoreCase));
                    
                    string detectionMethod = "route_template_analysis"; // Default
                    if (matchingParam is ParameterDescriptor paramDesc && paramDesc.BindingInfo?.BindingSource != null)
                    {
                        // Parameter has explicit binding information, mark as explicit
                        detectionMethod = "explicit";
                    }
                    
                    var routeSchema = new McpSchema
                    {
                        Type = MapDotNetTypeToJsonSchemaType(routeParam.Value),
                        Description = $"Route parameter {routeParam.Key}",
                        Source = "route",
                        IsRequired = true,
                        Annotations = new Dictionary<string, object>
                        {
                            ["source"] = "route",
                            ["isRouteParameter"] = true,
                            ["sourceDetectionMethod"] = detectionMethod,
                            ["httpMethod"] = httpMethod
                        }
                    };
                    
                    // ENHANCED: Add route template context
                    if (!string.IsNullOrEmpty(routeInfo.CombinedTemplate))
                    {
                        routeSchema.Annotations["routeTemplate"] = routeInfo.CombinedTemplate;
                    }
                    else if (!string.IsNullOrEmpty(actionDescriptor.AttributeRouteInfo?.Template))
                    {
                        routeSchema.Annotations["routeTemplate"] = actionDescriptor.AttributeRouteInfo.Template;
                    }
                    
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

                    // ENHANCED: Use v2.0 enhanced parameter source detection with comprehensive analysis
                    var (parameterSource, detectionMethod) = DetectParameterSourceEnhanced(parameter, actionDescriptor);
                    if (!string.IsNullOrEmpty(parameterSource))
                    {
                        paramSchema.Source = parameterSource;
                        paramSchema.Annotations ??= new Dictionary<string, object>();
                        paramSchema.Annotations["source"] = parameterSource;
                        
                        // ENHANCED: Preserve detection method metadata directly from enhanced detection
                        paramSchema.Annotations["sourceDetectionMethod"] = detectionMethod;
                        
                        // Route parameters get route-specific metadata
                        if (parameterSource == "route")
                        {
                            paramSchema.Annotations["isRouteParameter"] = true;
                        }
                        
                        // ENHANCED: Preserve HTTP method context for all parameters
                        paramSchema.Annotations["httpMethod"] = httpMethod;
                        
                        // ENHANCED: Preserve route template context for all parameters
                        if (!string.IsNullOrEmpty(routeInfo.CombinedTemplate))
                        {
                            paramSchema.Annotations["routeTemplate"] = routeInfo.CombinedTemplate;
                        }
                        else if (!string.IsNullOrEmpty(actionDescriptor.AttributeRouteInfo?.Template))
                        {
                            paramSchema.Annotations["routeTemplate"] = actionDescriptor.AttributeRouteInfo.Template;
                        }
                        
                        // ENHANCED: Preserve validation metadata for complex objects
                        if (IsComplexType(parameter.ParameterType))
                        {
                            var validationMetadata = ExtractValidationMetadata(parameter.ParameterType);
                            if (validationMetadata.Count > 0)
                            {
                                paramSchema.Annotations["validationRules"] = validationMetadata;
                                paramSchema.Annotations["parameterValidation"] = true;
                            }
                        }
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

            // Parse route template for parameters like {id}, {tenantId:int}, {customerId?}, etc.
            // Updated regex to handle optional parameters by stripping the '?' from parameter names
            var matches = System.Text.RegularExpressions.Regex.Matches(routeTemplate, @"\{([^}:?]+)(?:\?)?(?::[^}]+)?\}");
            
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
            // Handle nullable types by unwrapping them
            var underlyingType = Nullable.GetUnderlyingType(type);
            if (underlyingType != null)
            {
                type = underlyingType;
            }

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
        /// Enhanced parameter source detection with comprehensive route template analysis and HTTP method context.
        /// This is the core enhancement for MCPBuckle v2.0.
        /// </summary>
        private (string source, string detectionMethod) DetectParameterSourceEnhanced(dynamic parameter, ControllerActionDescriptor actionDescriptor)
        {
            // 1. ENHANCED: Explicit binding attributes (highest priority)
            // Cast to ParameterDescriptor to access BindingInfo properly
            if (parameter is ParameterDescriptor paramDesc && paramDesc.BindingInfo?.BindingSource != null)
            {
                var bindingSource = paramDesc.BindingInfo.BindingSource;
                var sourceId = bindingSource.Id?.ToLowerInvariant();
                
                // Check against known BindingSource constants
                if (bindingSource == Microsoft.AspNetCore.Mvc.ModelBinding.BindingSource.Body) return ("body", "explicit");
                if (bindingSource == Microsoft.AspNetCore.Mvc.ModelBinding.BindingSource.Query) return ("query", "explicit");
                if (bindingSource == Microsoft.AspNetCore.Mvc.ModelBinding.BindingSource.Header) return ("header", "explicit");
                if (bindingSource == Microsoft.AspNetCore.Mvc.ModelBinding.BindingSource.Path) return ("route", "explicit");
                
                // Fallback to ID-based check
                switch (sourceId)
                {
                    case "body": return ("body", "explicit");
                    case "query": return ("query", "explicit");
                    case "header": return ("header", "explicit");
                    case "route": return ("route", "explicit");
                    case "path": return ("route", "explicit");
                }
            }

            // 2. ENHANCED: Comprehensive ASP.NET Core binding attribute detection
            try
            {
                var methodInfo = actionDescriptor.MethodInfo;
                if (methodInfo != null)
                {
                    var methodParam = methodInfo.GetParameters()
                        .FirstOrDefault(p => p.Name == parameter.Name);
                    
                    if (methodParam != null)
                    {
                        // Check for explicit ASP.NET Core binding attributes
                        if (methodParam.GetCustomAttribute<FromRouteAttribute>() != null) return ("route", "explicit");
                        if (methodParam.GetCustomAttribute<FromBodyAttribute>() != null) return ("body", "explicit");
                        if (methodParam.GetCustomAttribute<FromQueryAttribute>() != null) return ("query", "explicit");
                        if (methodParam.GetCustomAttribute<FromHeaderAttribute>() != null) return ("header", "explicit");
                        if (methodParam.GetCustomAttribute<FromFormAttribute>() != null) return ("form", "explicit");
                    }
                }
            }
            catch (Exception ex)
            {
                if (_logger != null)
                {
                    LoggerExtensions.LogWarning(_logger, ex, "Error detecting explicit binding attributes for parameter {Parameter}", parameter.Name);
                }
            }

            // 3. ENHANCED: Route template analysis with comprehensive parsing
            var routeInfo = AnalyzeRouteTemplates(actionDescriptor);
            var parameterName = parameter.Name as string;
            if (parameterName != null && routeInfo.RouteParameters.Contains(parameterName, StringComparer.OrdinalIgnoreCase))
            {
                return ("route", "route_template_analysis");
            }

            // 4. ENHANCED: HTTP method + type-based inference using ASP.NET Core logic
            var httpMethod = GetHttpMethodFromActionDescriptor(actionDescriptor);
            var parameterType = parameter.ParameterType as Type;
            
            // Mirror ASP.NET Core's default binding behavior
            if (httpMethod == "GET" || httpMethod == "DELETE" || httpMethod == "HEAD")
            {
                // GET/DELETE methods: all parameters default to query (even complex objects with [FromQuery])
                return ("query", "http_method_inference");
            }
            
            // POST, PUT, PATCH - use type-based inference
            if (parameterType != null && IsComplexType(parameterType))
            {
                return ("body", "http_method_inference");  // Complex objects default to body for modification operations
            }
            
            // Primitive types default to query for all HTTP methods
            return ("query", "http_method_inference");
        }

        /// <summary>
        /// Enhanced route template analysis with controller and method-level route combination.
        /// </summary>
        private RouteAnalysisResult AnalyzeRouteTemplates(ControllerActionDescriptor actionDescriptor)
        {
            var result = new RouteAnalysisResult();
            
            try
            {
                // Analyze controller-level route template
                var controllerType = actionDescriptor.ControllerTypeInfo;
                var controllerRoute = controllerType.GetCustomAttribute<RouteAttribute>();
                if (controllerRoute?.Template != null)
                {
                    result.ControllerTemplate = controllerRoute.Template;
                    result.RouteParameters.AddRange(ExtractRouteParametersFromTemplate(controllerRoute.Template));
                }

                // Analyze method-level route templates from HTTP method attributes
                var methodInfo = actionDescriptor.MethodInfo;
                if (methodInfo != null)
                {
                    var httpAttributes = methodInfo.GetCustomAttributes()
                        .Where(attr => attr.GetType().Name.StartsWith("Http") && attr.GetType().Name.EndsWith("Attribute"))
                        .ToArray();

                    foreach (var httpAttr in httpAttributes)
                    {
                        // Try to get Template property using reflection
                        var templateProperty = httpAttr.GetType().GetProperty("Template");
                        if (templateProperty?.GetValue(httpAttr) is string template && !string.IsNullOrEmpty(template))
                        {
                            result.MethodTemplate = template;
                            result.RouteParameters.AddRange(ExtractRouteParametersFromTemplate(template));
                        }
                    }
                }

                // Use ActionDescriptor's AttributeRouteInfo as fallback
                if (string.IsNullOrEmpty(result.MethodTemplate) && actionDescriptor.AttributeRouteInfo?.Template != null)
                {
                    result.CombinedTemplate = actionDescriptor.AttributeRouteInfo.Template;
                    result.RouteParameters.AddRange(ExtractRouteParametersFromTemplate(actionDescriptor.AttributeRouteInfo.Template));
                }
                else
                {
                    // Combine controller and method templates
                    result.CombinedTemplate = CombineRouteTemplates(result.ControllerTemplate, result.MethodTemplate);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error analyzing route templates for action {Controller}.{Action}", 
                    actionDescriptor.ControllerName, actionDescriptor.ActionName);
            }
            
            return result;
        }

        /// <summary>
        /// Extract route parameters from a route template with enhanced regex parsing.
        /// </summary>
        private List<string> ExtractRouteParametersFromTemplate(string template)
        {
            if (string.IsNullOrEmpty(template))
                return new List<string>();

            // Enhanced regex to handle constraints, optional parameters, catch-all parameters
            // Updated to properly extract parameter name without optional '?' marker
            var regex = new System.Text.RegularExpressions.Regex(@"\{(\w+)(?:\?)?(?::[^}]*)?(?:\*[^}]*)?\}", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var matches = regex.Matches(template);
            
            return matches.Cast<System.Text.RegularExpressions.Match>()
                         .Select(m => m.Groups[1].Value)
                         .ToList();
        }

        /// <summary>
        /// Combine controller and method-level route templates.
        /// </summary>
        private string CombineRouteTemplates(string? controllerTemplate, string? methodTemplate)
        {
            if (string.IsNullOrEmpty(controllerTemplate) && string.IsNullOrEmpty(methodTemplate))
                return string.Empty;
                
            if (string.IsNullOrEmpty(controllerTemplate))
                return methodTemplate ?? string.Empty;
                
            if (string.IsNullOrEmpty(methodTemplate))
                return controllerTemplate;
                
            // Handle absolute method templates (starting with /)
            if (methodTemplate.StartsWith("/"))
                return methodTemplate;
                
            return $"{controllerTemplate.TrimEnd('/')}/{methodTemplate.TrimStart('/')}";
        }

        /// <summary>
        /// Extract HTTP method from action descriptor.
        /// </summary>
        private string GetHttpMethodFromActionDescriptor(ControllerActionDescriptor actionDescriptor)
        {
            try
            {
                var methodInfo = actionDescriptor.MethodInfo;
                if (methodInfo != null)
                {
                    var httpMethodAttribute = methodInfo.GetCustomAttributes()
                        .FirstOrDefault(a => a.GetType().Name.StartsWith("Http") && a.GetType().Name.EndsWith("Attribute"));

                    if (httpMethodAttribute != null)
                    {
                        return httpMethodAttribute.GetType().Name
                            .Replace("Http", "")
                            .Replace("Attribute", "")
                            .ToUpperInvariant();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error extracting HTTP method for action {Controller}.{Action}", 
                    actionDescriptor.ControllerName, actionDescriptor.ActionName);
            }
            
            return "GET"; // Safe default
        }

        /// <summary>
        /// Backward compatibility method - now delegates to enhanced detection.
        /// </summary>
        private static string DetectParameterSource(dynamic parameter, ControllerActionDescriptor actionDescriptor)
        {
            // For backward compatibility, create a temporary service instance
            // In practice, this method is being phased out in favor of the enhanced version
            return "query"; // Safe fallback
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

        /// <summary>
        /// Extracts validation metadata from a complex type for enhanced schema preservation.
        /// This is part of the MCPBuckle v2.0 enhancements.
        /// </summary>
        private Dictionary<string, object> ExtractValidationMetadata(Type type)
        {
            var metadata = new Dictionary<string, object>();
            
            try
            {
                var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                var validationRules = new Dictionary<string, object>();
                
                foreach (var prop in properties)
                {
                    var propRules = new Dictionary<string, object>();
                    
                    // Check for Required attribute
                    var requiredAttr = prop.GetCustomAttribute<System.ComponentModel.DataAnnotations.RequiredAttribute>();
                    if (requiredAttr != null)
                    {
                        propRules["required"] = true;
                        propRules["requiredMessage"] = requiredAttr.ErrorMessage ?? "This field is required";
                    }
                    
                    // Check for StringLength attribute
                    var stringLengthAttr = prop.GetCustomAttribute<System.ComponentModel.DataAnnotations.StringLengthAttribute>();
                    if (stringLengthAttr != null)
                    {
                        propRules["maxLength"] = stringLengthAttr.MaximumLength;
                        if (stringLengthAttr.MinimumLength > 0)
                        {
                            propRules["minLength"] = stringLengthAttr.MinimumLength;
                        }
                    }
                    
                    // Check for Range attribute
                    var rangeAttr = prop.GetCustomAttribute<System.ComponentModel.DataAnnotations.RangeAttribute>();
                    if (rangeAttr != null)
                    {
                        propRules["minimum"] = rangeAttr.Minimum;
                        propRules["maximum"] = rangeAttr.Maximum;
                    }
                    
                    // Check for EmailAddress attribute
                    var emailAttr = prop.GetCustomAttribute<System.ComponentModel.DataAnnotations.EmailAddressAttribute>();
                    if (emailAttr != null)
                    {
                        propRules["format"] = "email";
                    }
                    
                    if (propRules.Count > 0)
                    {
                        validationRules[prop.Name] = propRules;
                    }
                }
                
                if (validationRules.Count > 0)
                {
                    metadata["propertyValidation"] = validationRules;
                    metadata["hasValidation"] = true;
                }
            }
            catch (Exception ex)
            {
                if (_logger != null)
                {
                    LoggerExtensions.LogWarning(_logger, ex, "Error extracting validation metadata for type {TypeName}", type.Name);
                }
            }
            
            return metadata;
        }

        #endregion
    }

    /// <summary>
    /// Result of route template analysis for enhanced parameter source detection.
    /// </summary>
    internal class RouteAnalysisResult
    {
        public string? ControllerTemplate { get; set; }
        public string? MethodTemplate { get; set; }
        public string CombinedTemplate { get; set; } = string.Empty;
        public List<string> RouteParameters { get; set; } = new List<string>();
    }
}
