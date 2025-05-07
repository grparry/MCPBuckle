using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
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
                InputSchema = CreateInputSchema(actionDescriptor)
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

            // Check if there's a single complex type parameter that would be bound from the body
            var bodyParameter = actionDescriptor.Parameters
                .FirstOrDefault(p => 
                    // Check for [FromBody] attribute
                    p.BindingInfo?.BindingSource?.Id == "Body" ||
                    // Or check if it's a complex type (class) that would be implicitly bound from body
                    (p.ParameterType.IsClass && 
                     p.ParameterType != typeof(string) && 
                     !p.ParameterType.IsArray &&
                     !typeof(System.Collections.IEnumerable).IsAssignableFrom(p.ParameterType)));

            // If we have a body parameter that's a complex type, use its schema directly
            if (bodyParameter != null && bodyParameter.ParameterType.IsClass && bodyParameter.ParameterType != typeof(string))
            {
                // For a complex type bound from body, return its schema directly without nesting
                return _typeSchemaGenerator.GenerateSchema(bodyParameter.ParameterType);
            }

            // Otherwise, process all parameters normally
            foreach (var parameter in actionDescriptor.Parameters)
            {
                // Generate schema for parameter type
                var paramSchema = _typeSchemaGenerator.GenerateSchema(parameter.ParameterType);

                // Add parameter documentation if available
                if (_options.IncludeXmlDocumentation)
                {
                    var paramDescription = _xmlDocumentationService.GetParameterDocumentation(
                        actionDescriptor.ControllerTypeInfo,
                        actionDescriptor.MethodInfo,
                        parameter.Name);

                    if (!string.IsNullOrEmpty(paramDescription))
                    {
                        paramSchema.Description = paramDescription;
                    }
                }

                properties[parameter.Name] = paramSchema;

                // For simplicity in the MVP, mark all parameters as required
                // In a future version, we can add more sophisticated required field detection
                required.Add(parameter.Name);
            }

            return new McpSchema
            {
                Type = "object",
                Properties = properties,
                Required = required
            };
        }
    }
}
