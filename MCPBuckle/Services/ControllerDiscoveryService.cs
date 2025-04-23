using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Options;
using MCPBuckle.Configuration;
using MCPBuckle.Models;

namespace MCPBuckle.Services
{
    /// <summary>
    /// Service for discovering controllers and their actions in an ASP.NET Core application.
    /// </summary>
    public class ControllerDiscoveryService
    {
        private readonly IActionDescriptorCollectionProvider _actionDescriptorCollectionProvider;
        private readonly XmlDocumentationService _xmlDocumentationService;
        private readonly TypeSchemaGenerator _typeSchemaGenerator;
        private readonly McpBuckleOptions _options;

        public ControllerDiscoveryService(
            IActionDescriptorCollectionProvider actionDescriptorCollectionProvider,
            XmlDocumentationService xmlDocumentationService,
            TypeSchemaGenerator typeSchemaGenerator,
            IOptions<McpBuckleOptions> options)
        {
            _actionDescriptorCollectionProvider = actionDescriptorCollectionProvider;
            _xmlDocumentationService = xmlDocumentationService;
            _typeSchemaGenerator = typeSchemaGenerator;
            _options = options.Value;
        }

        /// <summary>
        /// Discovers all controllers and their actions in the application and converts them to MCP tools.
        /// </summary>
        /// <returns>A list of MCP tools representing the API endpoints.</returns>
        public List<McpTool> DiscoverTools()
        {
            var tools = new List<McpTool>();
            var actionDescriptors = _actionDescriptorCollectionProvider.ActionDescriptors.Items;

            foreach (var descriptor in actionDescriptors)
            {
                if (descriptor is ControllerActionDescriptor controllerActionDescriptor)
                {
                    // Skip if controller is excluded or not included
                    if (ShouldSkipController(controllerActionDescriptor.ControllerName))
                    {
                        continue;
                    }

                    var tool = CreateToolFromAction(controllerActionDescriptor);
                    if (tool != null)
                    {
                        tools.Add(tool);
                    }
                }
            }

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

            return tool;
        }

        private McpSchema CreateInputSchema(ControllerActionDescriptor actionDescriptor)
        {
            var properties = new Dictionary<string, McpSchema>();
            var required = new List<string>();

            // Process parameters
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
