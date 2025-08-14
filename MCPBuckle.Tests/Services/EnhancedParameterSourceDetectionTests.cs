using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using MCPBuckle.Configuration;
using MCPBuckle.Models;
using MCPBuckle.Services;

namespace MCPBuckle.Tests.Services
{
    /// <summary>
    /// Test-driven development tests for MCPBuckle v2.0 enhanced parameter source detection.
    /// These tests define the requirements for runtime interrogation-based parameter binding.
    /// </summary>
    public class EnhancedParameterSourceDetectionTests
    {
        private readonly Mock<IActionDescriptorCollectionProvider> _mockActionDescriptorProvider;
        private readonly Mock<XmlDocumentationService> _mockXmlDocService;
        private readonly Mock<TypeSchemaGenerator> _mockTypeSchemaGenerator;
        private readonly Mock<ILogger<ControllerDiscoveryService>> _mockLogger;
        private readonly McpBuckleOptions _defaultOptions;

        public EnhancedParameterSourceDetectionTests()
        {
            _mockActionDescriptorProvider = new Mock<IActionDescriptorCollectionProvider>();
            _mockXmlDocService = new Mock<XmlDocumentationService>(MockBehavior.Loose);
            _mockTypeSchemaGenerator = new Mock<TypeSchemaGenerator>(_mockXmlDocService.Object, Options.Create(new McpBuckleOptions()));
            _mockLogger = new Mock<ILogger<ControllerDiscoveryService>>();
            
            _defaultOptions = new McpBuckleOptions
            {
                IncludeControllerNameInToolName = false,
                IncludeXmlDocumentation = true
            };

            // Setup mock schema generation
            _mockTypeSchemaGenerator.Setup(g => g.GenerateSchema(It.IsAny<Type>()))
                .Returns(new McpSchema { Type = "object" });
        }

        #region Route Template Analysis Tests

        [Fact]
        public void EnhancedParameterSourceDetection_WithControllerRouteParameter_DetectsRouteSource()
        {
            // Arrange
            var actionDescriptors = CreateActionDescriptor(
                typeof(RouteTestController),
                nameof(RouteTestController.GetByOrgAndUser),
                new[] 
                {
                    CreateParameterDescriptor("orgId", typeof(int)),
                    CreateParameterDescriptor("userId", typeof(int)),
                    CreateParameterDescriptor("includeDetails", typeof(bool))
                },
                "api/organizations/{orgId}/users/{userId}"
            );

            var service = CreateControllerDiscoveryService();
            _mockActionDescriptorProvider.Setup(p => p.ActionDescriptors)
                .Returns(new ActionDescriptorCollection(actionDescriptors, 1));

            // Act
            var tools = service.DiscoverTools();

            // Assert
            Assert.Single(tools);
            var tool = tools.First();
            var inputSchema = tool.InputSchema;
            
            // Route parameters should be detected from template
            Assert.Contains("orgId", inputSchema.Properties.Keys);
            Assert.Equal("route", inputSchema.Properties["orgId"].Source);
            Assert.True(inputSchema.Properties["orgId"].Annotations?.ContainsKey("source") == true);
            Assert.Equal("route", inputSchema.Properties["orgId"].Annotations?["source"]);
            
            Assert.Contains("userId", inputSchema.Properties.Keys);
            Assert.Equal("route", inputSchema.Properties["userId"].Source);
            
            // Query parameter should be detected as query
            Assert.Contains("includeDetails", inputSchema.Properties.Keys);
            Assert.Equal("query", inputSchema.Properties["includeDetails"].Source);
        }

        [Fact]
        public void EnhancedParameterSourceDetection_WithMethodRouteParameter_DetectsRouteSource()
        {
            // Arrange
            var actionDescriptors = CreateActionDescriptor(
                typeof(RouteTestController),
                nameof(RouteTestController.GetWithMethodRoute),
                new[] { CreateParameterDescriptor("id", typeof(int)) },
                null, // No controller route
                "items/{id}" // Method-level route
            );

            var service = CreateControllerDiscoveryService();
            _mockActionDescriptorProvider.Setup(p => p.ActionDescriptors)
                .Returns(new ActionDescriptorCollection(actionDescriptors, 1));

            // Act
            var tools = service.DiscoverTools();

            // Assert
            Assert.Single(tools);
            var tool = tools.First();
            
            Assert.Contains("id", tool.InputSchema.Properties.Keys);
            Assert.Equal("route", tool.InputSchema.Properties["id"].Source);
        }

        [Fact]
        public void EnhancedParameterSourceDetection_WithRouteConstraints_DetectsRouteParameters()
        {
            // Arrange
            var actionDescriptors = CreateActionDescriptor(
                typeof(RouteTestController),
                nameof(RouteTestController.GetWithConstraints),
                new[] 
                {
                    CreateParameterDescriptor("orgId", typeof(int)),
                    CreateParameterDescriptor("userGuid", typeof(Guid)),
                    CreateParameterDescriptor("version", typeof(string))
                },
                "api/organizations/{orgId:int}/users/{userGuid:guid}/version/{version:alpha?}"
            );

            var service = CreateControllerDiscoveryService();
            _mockActionDescriptorProvider.Setup(p => p.ActionDescriptors)
                .Returns(new ActionDescriptorCollection(actionDescriptors, 1));

            // Act
            var tools = service.DiscoverTools();

            // Assert
            var tool = tools.First();
            var inputSchema = tool.InputSchema;
            
            // All route parameters with constraints should be detected
            Assert.Equal("route", inputSchema.Properties["orgId"].Source);
            Assert.Equal("route", inputSchema.Properties["userGuid"].Source);
            Assert.Equal("route", inputSchema.Properties["version"].Source);
            
            // Verify route metadata is preserved
            Assert.True(inputSchema.Properties["orgId"].Annotations?.ContainsKey("isRouteParameter") == true);
            Assert.True(inputSchema.Properties["orgId"].Annotations?.ContainsKey("routeTemplate") == true);
        }

        #endregion

        #region HTTP Method Context Analysis Tests

        [Fact]
        public void EnhancedParameterSourceDetection_WithGetMethod_InfersQueryForPrimitives()
        {
            // Arrange
            var actionDescriptors = CreateActionDescriptor(
                typeof(HttpMethodTestController),
                nameof(HttpMethodTestController.GetWithPrimitives),
                new[] 
                {
                    CreateParameterDescriptor("filter", typeof(string)),
                    CreateParameterDescriptor("pageSize", typeof(int)),
                    CreateParameterDescriptor("isActive", typeof(bool))
                },
                "api/items"
            );

            var service = CreateControllerDiscoveryService();
            _mockActionDescriptorProvider.Setup(p => p.ActionDescriptors)
                .Returns(new ActionDescriptorCollection(actionDescriptors, 1));

            // Act
            var tools = service.DiscoverTools();

            // Assert
            var tool = tools.First();
            var inputSchema = tool.InputSchema;
            
            // GET method with primitives should default to query
            Assert.Equal("query", inputSchema.Properties["filter"].Source);
            Assert.Equal("query", inputSchema.Properties["pageSize"].Source);
            Assert.Equal("query", inputSchema.Properties["isActive"].Source);
            
            // HTTP method context should be preserved
            Assert.Equal("GET", inputSchema.Properties["filter"].Annotations?["httpMethod"]);
        }

        [Fact]
        public void EnhancedParameterSourceDetection_WithPostMethod_InfersBodyForComplexTypes()
        {
            // Arrange
            var actionDescriptors = CreateActionDescriptor(
                typeof(HttpMethodTestController),
                nameof(HttpMethodTestController.PostWithComplexObject),
                new[] 
                {
                    CreateParameterDescriptor("model", typeof(ComplexRequestModel)),
                    CreateParameterDescriptor("version", typeof(string))
                },
                "api/items"
            );

            var service = CreateControllerDiscoveryService();
            _mockActionDescriptorProvider.Setup(p => p.ActionDescriptors)
                .Returns(new ActionDescriptorCollection(actionDescriptors, 1));

            // Act
            var tools = service.DiscoverTools();

            // Assert
            var tool = tools.First();
            var inputSchema = tool.InputSchema;
            
            // POST method: complex types default to body, primitives to query
            Assert.Equal("body", inputSchema.Properties["model"].Source);
            Assert.Equal("query", inputSchema.Properties["version"].Source);
            
            // HTTP method context should be preserved
            Assert.Equal("POST", inputSchema.Properties["model"].Annotations?["httpMethod"]);
        }

        [Fact]
        public void EnhancedParameterSourceDetection_WithDeleteMethod_InfersQueryForAllTypes()
        {
            // Arrange
            var actionDescriptors = CreateActionDescriptor(
                typeof(HttpMethodTestController),
                nameof(HttpMethodTestController.DeleteWithParameters),
                new[] 
                {
                    CreateParameterDescriptor("reason", typeof(string)),
                    CreateParameterDescriptor("force", typeof(bool))
                },
                "api/items/{id}"
            );

            var service = CreateControllerDiscoveryService();
            _mockActionDescriptorProvider.Setup(p => p.ActionDescriptors)
                .Returns(new ActionDescriptorCollection(actionDescriptors, 1));

            // Act
            var tools = service.DiscoverTools();

            // Assert
            var tool = tools.First();
            var inputSchema = tool.InputSchema;
            
            // DELETE method: all non-route parameters should default to query
            Assert.Equal("query", inputSchema.Properties["reason"].Source);
            Assert.Equal("query", inputSchema.Properties["force"].Source);
        }

        #endregion

        #region Explicit Binding Attribute Detection Tests

        [Fact]
        public void EnhancedParameterSourceDetection_WithExplicitFromAttributes_RespectsExplicitBinding()
        {
            // Arrange
            var actionDescriptors = CreateActionDescriptor(
                typeof(ExplicitBindingTestController),
                nameof(ExplicitBindingTestController.PostWithExplicitBinding),
                new[] 
                {
                    CreateParameterDescriptor("orgId", typeof(int), bindingSource: BindingSource.Path),
                    CreateParameterDescriptor("model", typeof(ComplexRequestModel), bindingSource: BindingSource.Body),
                    CreateParameterDescriptor("apiVersion", typeof(string), bindingSource: BindingSource.Header),
                    CreateParameterDescriptor("includeDebug", typeof(bool), bindingSource: BindingSource.Query)
                },
                "api/organizations/{orgId}/items"
            );

            var service = CreateControllerDiscoveryService();
            _mockActionDescriptorProvider.Setup(p => p.ActionDescriptors)
                .Returns(new ActionDescriptorCollection(actionDescriptors, 1));

            // Act
            var tools = service.DiscoverTools();

            // Assert
            var tool = tools.First();
            var inputSchema = tool.InputSchema;
            
            // Explicit binding attributes should always be respected
            Assert.Equal("route", inputSchema.Properties["orgId"].Source);
            Assert.Equal("body", inputSchema.Properties["model"].Source);
            Assert.Equal("header", inputSchema.Properties["apiVersion"].Source);
            Assert.Equal("query", inputSchema.Properties["includeDebug"].Source);
            
            // Source detection method should be marked as explicit
            Assert.Equal("explicit", inputSchema.Properties["orgId"].Annotations?["sourceDetectionMethod"]);
            Assert.Equal("explicit", inputSchema.Properties["model"].Annotations?["sourceDetectionMethod"]);
        }

        [Fact]
        public void EnhancedParameterSourceDetection_WithFromQueryComplexObject_OverridesInference()
        {
            // Arrange - Complex object with [FromQuery] should override default body inference
            var actionDescriptors = CreateActionDescriptor(
                typeof(ExplicitBindingTestController),
                nameof(ExplicitBindingTestController.GetWithComplexQuery),
                new[] 
                {
                    CreateParameterDescriptor("searchRequest", typeof(ComplexRequestModel), bindingSource: BindingSource.Query)
                },
                "api/search"
            );

            var service = CreateControllerDiscoveryService();
            _mockActionDescriptorProvider.Setup(p => p.ActionDescriptors)
                .Returns(new ActionDescriptorCollection(actionDescriptors, 1));

            // Act
            var tools = service.DiscoverTools();

            // Assert
            var tool = tools.First();
            
            // Complex object with [FromQuery] should be query, not body
            Assert.Equal("query", tool.InputSchema.Properties["searchRequest"].Source);
            Assert.Equal("explicit", tool.InputSchema.Properties["searchRequest"].Annotations?["sourceDetectionMethod"]);
        }

        #endregion

        #region Schema Metadata Preservation Tests

        [Fact]
        public void EnhancedParameterSourceDetection_PreservesValidationMetadata()
        {
            // Arrange
            var actionDescriptors = CreateActionDescriptor(
                typeof(ValidationTestController),
                nameof(ValidationTestController.CreateWithValidation),
                new[] 
                {
                    CreateParameterDescriptor("model", typeof(ValidatedRequestModel))
                },
                "api/items"
            );

            var service = CreateControllerDiscoveryService();
            _mockActionDescriptorProvider.Setup(p => p.ActionDescriptors)
                .Returns(new ActionDescriptorCollection(actionDescriptors, 1));

            // Act
            var tools = service.DiscoverTools();

            // Assert
            var tool = tools.First();
            var inputSchema = tool.InputSchema;
            var modelProperty = inputSchema.Properties["model"];
            
            // Validation metadata should be preserved
            Assert.True(modelProperty.Annotations?.ContainsKey("validationRules") == true);
            Assert.True(modelProperty.Annotations?.ContainsKey("parameterValidation") == true);
            
            // Required properties should be identified
            Assert.Contains("Name", modelProperty.Required);
            Assert.Contains("Email", modelProperty.Required);
        }

        [Fact]
        public void EnhancedParameterSourceDetection_PreservesRouteTemplateContext()
        {
            // Arrange
            var actionDescriptors = CreateActionDescriptor(
                typeof(RouteTestController),
                nameof(RouteTestController.GetByOrgAndUser),
                new[] 
                {
                    CreateParameterDescriptor("orgId", typeof(int)),
                    CreateParameterDescriptor("userId", typeof(int))
                },
                "api/organizations/{orgId}/users/{userId}"
            );

            var service = CreateControllerDiscoveryService();
            _mockActionDescriptorProvider.Setup(p => p.ActionDescriptors)
                .Returns(new ActionDescriptorCollection(actionDescriptors, 1));

            // Act
            var tools = service.DiscoverTools();

            // Assert
            var tool = tools.First();
            var inputSchema = tool.InputSchema;
            
            // Route template context should be preserved for all parameters
            foreach (var param in inputSchema.Properties.Values)
            {
                Assert.True(param.Annotations?.ContainsKey("routeTemplate") == true);
                Assert.Equal("api/organizations/{orgId}/users/{userId}", param.Annotations?["routeTemplate"]);
            }
        }

        [Fact]
        public void EnhancedParameterSourceDetection_PreservesHttpMethodContext()
        {
            // Arrange
            var actionDescriptors = CreateActionDescriptor(
                typeof(HttpMethodTestController),
                nameof(HttpMethodTestController.PutWithUpdate),
                new[] 
                {
                    CreateParameterDescriptor("id", typeof(int)),
                    CreateParameterDescriptor("model", typeof(ComplexRequestModel))
                },
                "api/items/{id}"
            );

            var service = CreateControllerDiscoveryService();
            _mockActionDescriptorProvider.Setup(p => p.ActionDescriptors)
                .Returns(new ActionDescriptorCollection(actionDescriptors, 1));

            // Act
            var tools = service.DiscoverTools();

            // Assert
            var tool = tools.First();
            var inputSchema = tool.InputSchema;
            
            // HTTP method context should be preserved for all parameters
            foreach (var param in inputSchema.Properties.Values)
            {
                Assert.True(param.Annotations?.ContainsKey("httpMethod") == true);
                Assert.Equal("PUT", param.Annotations?["httpMethod"]);
            }
        }

        #endregion

        #region Integration Tests with Generic Web API Patterns

        [Fact]
        public void EnhancedParameterSourceDetection_BusinessWorkflowPattern_OrderExecution()
        {
            // Arrange - Generic business workflow execution pattern
            var actionDescriptors = CreateActionDescriptor(
                typeof(GenericBusinessPatternController),
                nameof(GenericBusinessPatternController.ProcessOrder),
                new[] 
                {
                    CreateParameterDescriptor("request", typeof(OrderProcessingRequest), bindingSource: BindingSource.Body)
                },
                "api/orders/process"
            );

            var service = CreateControllerDiscoveryService();
            _mockActionDescriptorProvider.Setup(p => p.ActionDescriptors)
                .Returns(new ActionDescriptorCollection(actionDescriptors, 1));

            // Act
            var tools = service.DiscoverTools();

            // Assert
            var tool = tools.First();
            var inputSchema = tool.InputSchema;
            
            // Should correctly identify body parameter with full context
            Assert.Equal("body", inputSchema.Properties["request"].Source);
            Assert.Equal("explicit", inputSchema.Properties["request"].Annotations?["sourceDetectionMethod"]);
            Assert.Equal("POST", inputSchema.Properties["request"].Annotations?["httpMethod"]);
        }

        [Fact]
        public void EnhancedParameterSourceDetection_NestedResourcePattern_UserOrders()
        {
            // Arrange - Generic nested resource pattern (organizations/users/orders)
            var actionDescriptors = CreateActionDescriptor(
                typeof(GenericBusinessPatternController),
                nameof(GenericBusinessPatternController.GetUserOrders),
                new[] 
                {
                    CreateParameterDescriptor("orgId", typeof(int)),
                    CreateParameterDescriptor("userId", typeof(int)),
                    CreateParameterDescriptor("pageNumber", typeof(int)),
                    CreateParameterDescriptor("pageSize", typeof(int)),
                    CreateParameterDescriptor("sortBy", typeof(string)),
                    CreateParameterDescriptor("minAmount", typeof(decimal?)),
                    CreateParameterDescriptor("maxAmount", typeof(decimal?))
                },
                "api/organizations/{orgId}/users/{userId}/orders"
            );

            var service = CreateControllerDiscoveryService();
            _mockActionDescriptorProvider.Setup(p => p.ActionDescriptors)
                .Returns(new ActionDescriptorCollection(actionDescriptors, 1));

            // Act
            var tools = service.DiscoverTools();

            // Assert
            var tool = tools.First();
            var inputSchema = tool.InputSchema;
            
            // Route parameters should be correctly identified
            Assert.Equal("route", inputSchema.Properties["orgId"].Source);
            Assert.Equal("route", inputSchema.Properties["userId"].Source);
            
            // Query parameters should be correctly identified
            Assert.Equal("query", inputSchema.Properties["pageNumber"].Source);
            Assert.Equal("query", inputSchema.Properties["pageSize"].Source);
            Assert.Equal("query", inputSchema.Properties["sortBy"].Source);
            Assert.Equal("query", inputSchema.Properties["minAmount"].Source);
            Assert.Equal("query", inputSchema.Properties["maxAmount"].Source);
            
            // All parameters should have consistent metadata
            foreach (var param in inputSchema.Properties.Values)
            {
                Assert.True(param.Annotations?.ContainsKey("sourceDetectionMethod") == true);
                Assert.True(param.Annotations?.ContainsKey("httpMethod") == true);
                Assert.True(param.Annotations?.ContainsKey("routeTemplate") == true);
            }
        }

        #endregion

        #region Performance and Edge Case Tests

        [Fact]
        public void EnhancedParameterSourceDetection_WithCircularReference_HandlesGracefully()
        {
            // Arrange
            var actionDescriptors = CreateActionDescriptor(
                typeof(EdgeCaseTestController),
                nameof(EdgeCaseTestController.PostWithCircularRef),
                new[] 
                {
                    CreateParameterDescriptor("model", typeof(CircularRefModel))
                },
                "api/circular"
            );

            var service = CreateControllerDiscoveryService();
            _mockActionDescriptorProvider.Setup(p => p.ActionDescriptors)
                .Returns(new ActionDescriptorCollection(actionDescriptors, 1));

            // Act & Assert - Should not throw exception
            var tools = service.DiscoverTools();
            
            Assert.Single(tools);
            var tool = tools.First();
            Assert.NotNull(tool.InputSchema);
            Assert.Equal("body", tool.InputSchema.Properties["model"].Source);
        }

        [Fact]
        public void EnhancedParameterSourceDetection_WithNullableTypes_DetectsCorrectly()
        {
            // Arrange
            var actionDescriptors = CreateActionDescriptor(
                typeof(EdgeCaseTestController),
                nameof(EdgeCaseTestController.GetWithNullables),
                new[] 
                {
                    CreateParameterDescriptor("id", typeof(int?)),
                    CreateParameterDescriptor("date", typeof(DateTime?)),
                    CreateParameterDescriptor("isActive", typeof(bool?))
                },
                "api/nullable/{id?}"
            );

            var service = CreateControllerDiscoveryService();
            _mockActionDescriptorProvider.Setup(p => p.ActionDescriptors)
                .Returns(new ActionDescriptorCollection(actionDescriptors, 1));

            // Act
            var tools = service.DiscoverTools();

            // Assert
            var tool = tools.First();
            var inputSchema = tool.InputSchema;
            
            // Nullable route parameter should still be detected as route
            Assert.Equal("route", inputSchema.Properties["id"].Source);
            // Nullable query parameters should be query
            Assert.Equal("query", inputSchema.Properties["date"].Source);
            Assert.Equal("query", inputSchema.Properties["isActive"].Source);
        }

        #endregion

        #region Helper Methods

        private ControllerDiscoveryService CreateControllerDiscoveryService(McpBuckleOptions? options = null)
        {
            options ??= _defaultOptions;
            return new ControllerDiscoveryService(
                _mockActionDescriptorProvider.Object,
                _mockXmlDocService.Object,
                _mockTypeSchemaGenerator.Object,
                Options.Create(options),
                _mockLogger.Object);
        }

        private List<ControllerActionDescriptor> CreateActionDescriptor(
            Type controllerType,
            string methodName,
            ParameterDescriptor[]? parameters,
            string? routeTemplate = null,
            string? methodRouteTemplate = null)
        {
            var methodInfo = controllerType.GetMethod(methodName);
            if (methodInfo == null)
                throw new InvalidOperationException($"Method {methodName} not found on {controllerType.Name}");

            var actionDescriptor = new ControllerActionDescriptor
            {
                ControllerName = controllerType.Name.Replace("Controller", ""),
                ActionName = methodName,
                ControllerTypeInfo = controllerType.GetTypeInfo(),
                MethodInfo = methodInfo,
                Parameters = parameters?.ToList() ?? new List<ParameterDescriptor>()
            };

            if (!string.IsNullOrEmpty(routeTemplate))
            {
                actionDescriptor.AttributeRouteInfo = new AttributeRouteInfo
                {
                    Template = routeTemplate
                };
            }

            return new List<ControllerActionDescriptor> { actionDescriptor };
        }

        private ControllerParameterDescriptor CreateParameterDescriptor(
            string name, 
            Type parameterType, 
            BindingSource? bindingSource = null)
        {
            var descriptor = new ControllerParameterDescriptor
            {
                Name = name,
                ParameterType = parameterType
            };

            if (bindingSource != null)
            {
                descriptor.BindingInfo = new BindingInfo
                {
                    BindingSource = bindingSource
                };
            }

            return descriptor;
        }

        #endregion
    }

    #region Test Controller Classes

    public class RouteTestController : ControllerBase
    {
        [HttpGet]
        public IActionResult GetByOrgAndUser(int orgId, int userId, bool includeDetails = false) => Ok();

        [HttpGet("items/{id}")]
        public IActionResult GetWithMethodRoute(int id) => Ok();

        [HttpGet]
        public IActionResult GetWithConstraints(int orgId, Guid userGuid, string version) => Ok();
    }

    public class HttpMethodTestController : ControllerBase
    {
        [HttpGet]
        public IActionResult GetWithPrimitives(string filter, int pageSize, bool isActive) => Ok();

        [HttpPost]
        public IActionResult PostWithComplexObject(ComplexRequestModel model, string version) => Ok();

        [HttpDelete]
        public IActionResult DeleteWithParameters(string reason, bool force) => Ok();

        [HttpPut]
        public IActionResult PutWithUpdate(int id, ComplexRequestModel model) => Ok();
    }

    public class ExplicitBindingTestController : ControllerBase
    {
        [HttpPost]
        public IActionResult PostWithExplicitBinding(
            [FromRoute] int orgId,
            [FromBody] ComplexRequestModel model,
            [FromHeader] string apiVersion,
            [FromQuery] bool includeDebug) => Ok();

        [HttpGet]
        public IActionResult GetWithComplexQuery([FromQuery] ComplexRequestModel searchRequest) => Ok();
    }

    public class ValidationTestController : ControllerBase
    {
        [HttpPost]
        public IActionResult CreateWithValidation(ValidatedRequestModel model) => Ok();
    }

    public class GenericBusinessPatternController : ControllerBase
    {
        [HttpPost]
        public IActionResult ProcessOrder([FromBody] OrderProcessingRequest request) => Ok();

        [HttpGet]
        public IActionResult GetUserOrders(
            int orgId, 
            int userId, 
            int pageNumber, 
            int pageSize, 
            string sortBy,
            decimal? minAmount,
            decimal? maxAmount) => Ok();
    }

    public class EdgeCaseTestController : ControllerBase
    {
        [HttpPost]
        public IActionResult PostWithCircularRef(CircularRefModel model) => Ok();

        [HttpGet]
        public IActionResult GetWithNullables(int? id, DateTime? date, bool? isActive) => Ok();
    }

    #endregion

    #region Test Model Classes

    public class ComplexRequestModel
    {
        public string Name { get; set; } = string.Empty;
        public int Priority { get; set; }
        public List<string> Tags { get; set; } = new();
    }

    public class ValidatedRequestModel
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Range(18, 120)]
        public int? Age { get; set; }
    }

    public class WorkflowExecutionRequest
    {
        [Required]
        public string WorkflowName { get; set; } = string.Empty;
        
        [Required]
        public string WorkflowVersion { get; set; } = string.Empty;
        
        [Required]
        public int TenantId { get; set; }
        
        [Required] 
        public int CustomerId { get; set; }
        
        public Dictionary<string, object> Parameters { get; set; } = new();
    }

    public class OrderProcessingRequest
    {
        [Required]
        public string OrderType { get; set; } = string.Empty;
        
        [Required]
        public string Priority { get; set; } = string.Empty;
        
        [Required]
        public int OrgId { get; set; }
        
        [Required] 
        public int UserId { get; set; }
        
        public Dictionary<string, object> OrderData { get; set; } = new();
    }

    public class CircularRefModel
    {
        public string Name { get; set; } = string.Empty;
        public CircularRefModel? Parent { get; set; }
        public List<CircularRefModel> Children { get; set; } = new();
    }

    #endregion
}