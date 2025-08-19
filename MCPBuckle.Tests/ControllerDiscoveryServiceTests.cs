using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using MCPBuckle.Attributes;
using MCPBuckle.Configuration;
using MCPBuckle.Models;
using MCPBuckle.Services;

namespace MCPBuckle.Tests
{
    public class ControllerDiscoveryServiceTests
    {
        private readonly Mock<IActionDescriptorCollectionProvider> _mockActionDescriptorProvider;
        private readonly Mock<XmlDocumentationService> _mockXmlDocService;
        private readonly Mock<TypeSchemaGenerator> _mockTypeSchemaGenerator;
        private readonly Mock<ILogger<ControllerDiscoveryService>> _mockLogger;
        private readonly McpBuckleOptions _defaultOptions;

        public ControllerDiscoveryServiceTests()
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
        }

        [Fact]
        public void DiscoverTools_WithValidController_ReturnsExpectedTools()
        {
            // Arrange
            var actionDescriptors = CreateTestActionDescriptors();
            _mockActionDescriptorProvider.Setup(p => p.ActionDescriptors)
                .Returns(new ActionDescriptorCollection(actionDescriptors, 1));

            var mockSchema = new McpSchema { Type = "string" };
            _mockTypeSchemaGenerator.Setup(g => g.GenerateSchema(It.IsAny<Type>()))
                .Returns(mockSchema);

            var service = CreateControllerDiscoveryService();

            // Act
            var tools = service.DiscoverTools();

            // Assert
            Assert.Single(tools);
            var tool = tools.First();
            Assert.Equal("GetUser", tool.Name);
            Assert.Contains("get", tool.Description);
            Assert.NotNull(tool.InputSchema);
            Assert.NotNull(tool.OutputSchema);
            Assert.Contains("HandlerTypeAssemblyQualifiedName", tool.Annotations.Keys);
            Assert.Contains("MethodName", tool.Annotations.Keys);
        }

        [Fact]
        public void DiscoverTools_WithControllerExcludeAttribute_SkipsController()
        {
            // Arrange
            var actionDescriptors = CreateTestActionDescriptorsWithExcludeAttribute();
            _mockActionDescriptorProvider.Setup(p => p.ActionDescriptors)
                .Returns(new ActionDescriptorCollection(actionDescriptors, 1));

            var service = CreateControllerDiscoveryService();

            // Act
            var tools = service.DiscoverTools();

            // Assert
            Assert.Empty(tools);
        }

        [Fact]
        public void DiscoverTools_WithMethodExcludeAttribute_SkipsMethod()
        {
            // Arrange
            var actionDescriptors = CreateTestActionDescriptorsWithMethodExclude();
            _mockActionDescriptorProvider.Setup(p => p.ActionDescriptors)
                .Returns(new ActionDescriptorCollection(actionDescriptors, 1));

            var service = CreateControllerDiscoveryService();

            // Act
            var tools = service.DiscoverTools();

            // Assert
            Assert.Empty(tools);
        }

        [Fact]
        public void DiscoverTools_WithIncludeControllerNameOption_IncludesControllerInToolName()
        {
            // Arrange
            var options = new McpBuckleOptions
            {
                IncludeControllerNameInToolName = true
            };
            
            var actionDescriptors = CreateTestActionDescriptors();
            _mockActionDescriptorProvider.Setup(p => p.ActionDescriptors)
                .Returns(new ActionDescriptorCollection(actionDescriptors, 1));

            var mockSchema = new McpSchema { Type = "string" };
            _mockTypeSchemaGenerator.Setup(g => g.GenerateSchema(It.IsAny<Type>()))
                .Returns(mockSchema);

            var service = CreateControllerDiscoveryService(options);

            // Act
            var tools = service.DiscoverTools();

            // Assert
            Assert.Single(tools);
            Assert.Equal("TestController_GetUser", tools.First().Name);
        }

        [Fact]
        public void DiscoverTools_WithCustomToolNameFactory_UsesCustomNaming()
        {
            // Arrange
            var options = new McpBuckleOptions
            {
                CustomToolNameFactory = (controller, action) => $"custom_{controller}_{action}"
            };
            
            var actionDescriptors = CreateTestActionDescriptors();
            _mockActionDescriptorProvider.Setup(p => p.ActionDescriptors)
                .Returns(new ActionDescriptorCollection(actionDescriptors, 1));

            var mockSchema = new McpSchema { Type = "string" };
            _mockTypeSchemaGenerator.Setup(g => g.GenerateSchema(It.IsAny<Type>()))
                .Returns(mockSchema);

            var service = CreateControllerDiscoveryService(options);

            // Act
            var tools = service.DiscoverTools();

            // Assert
            Assert.Single(tools);
            Assert.Equal("custom_TestController_GetUser", tools.First().Name);
        }

        [Fact]
        public void DiscoverTools_WithExcludeControllers_SkipsExcludedControllers()
        {
            // Arrange
            var options = new McpBuckleOptions
            {
                ExcludeControllers = new List<string> { "TestController" }
            };
            
            var actionDescriptors = CreateTestActionDescriptors();
            _mockActionDescriptorProvider.Setup(p => p.ActionDescriptors)
                .Returns(new ActionDescriptorCollection(actionDescriptors, 1));

            var service = CreateControllerDiscoveryService(options);

            // Act
            var tools = service.DiscoverTools();

            // Assert
            Assert.Empty(tools);
        }

        [Fact]
        public void DiscoverTools_WithIncludeControllers_OnlyIncludesSpecifiedControllers()
        {
            // Arrange
            var options = new McpBuckleOptions
            {
                IncludeControllers = new List<string> { "OtherController" }
            };
            
            var actionDescriptors = CreateTestActionDescriptors();
            _mockActionDescriptorProvider.Setup(p => p.ActionDescriptors)
                .Returns(new ActionDescriptorCollection(actionDescriptors, 1));

            var service = CreateControllerDiscoveryService(options);

            // Act
            var tools = service.DiscoverTools();

            // Assert
            Assert.Empty(tools);
        }

        [Fact]
        public void DiscoverTools_WithComplexParameters_CreatesCorrectInputSchema()
        {
            // Arrange
            var actionDescriptors = CreateTestActionDescriptorsWithComplexParameters();
            _mockActionDescriptorProvider.Setup(p => p.ActionDescriptors)
                .Returns(new ActionDescriptorCollection(actionDescriptors, 1));

            var stringSchema = new McpSchema { Type = "string" };
            var intSchema = new McpSchema { Type = "integer" };
            _mockTypeSchemaGenerator.Setup(g => g.GenerateSchema(typeof(string))).Returns(stringSchema);
            _mockTypeSchemaGenerator.Setup(g => g.GenerateSchema(typeof(int))).Returns(intSchema);

            var service = CreateControllerDiscoveryService();

            // Act
            var tools = service.DiscoverTools();

            // Assert
            Assert.Single(tools);
            var tool = tools.First();
            Assert.NotNull(tool.InputSchema);
            Assert.NotNull(tool.InputSchema.Properties);
            Assert.True(tool.InputSchema.Properties.ContainsKey("id"));
            Assert.True(tool.InputSchema.Properties.ContainsKey("name"));
            Assert.Contains("id", tool.InputSchema.Required);
            Assert.Contains("name", tool.InputSchema.Required);
        }

        [Fact]
        public void DiscoverTools_WithBodyParameter_PreservesParameterName()
        {
            // Arrange
            var actionDescriptors = CreateTestActionDescriptorsWithBodyParameter();
            _mockActionDescriptorProvider.Setup(p => p.ActionDescriptors)
                .Returns(new ActionDescriptorCollection(actionDescriptors, 1));

            var objectSchema = new McpSchema { Type = "object" };
            _mockTypeSchemaGenerator.Setup(g => g.GenerateSchema(It.IsAny<Type>())).Returns(objectSchema);

            var service = CreateControllerDiscoveryService();

            // Act
            var tools = service.DiscoverTools();

            // Assert
            Assert.Single(tools);
            var tool = tools.First();
            Assert.NotNull(tool.InputSchema);
            Assert.NotNull(tool.InputSchema.Properties);
            Assert.True(tool.InputSchema.Properties.ContainsKey("model"));
            Assert.Contains("model", tool.InputSchema.Required);
        }

        [Fact]
        public void DiscoverTools_WithAsyncAction_HandlesTaskReturnType()
        {
            // Arrange
            var actionDescriptors = CreateTestActionDescriptorsWithAsyncMethod();
            _mockActionDescriptorProvider.Setup(p => p.ActionDescriptors)
                .Returns(new ActionDescriptorCollection(actionDescriptors, 1));

            var stringSchema = new McpSchema { Type = "string" };
            _mockTypeSchemaGenerator.Setup(g => g.GenerateSchema(typeof(string))).Returns(stringSchema);

            var service = CreateControllerDiscoveryService();

            // Act
            var tools = service.DiscoverTools();

            // Assert
            Assert.Single(tools);
            var tool = tools.First();
            Assert.NotNull(tool.OutputSchema);
            Assert.Equal("string", tool.OutputSchema.Type);
        }

        [Fact]
        public void DiscoverTools_WithFromQueryComplexObject_DetectsQuerySource()
        {
            // Arrange - This tests Level 1 fix: [FromQuery] detection for complex objects
            var actionDescriptors = CreateTestActionDescriptorsWithFromQueryComplex();
            _mockActionDescriptorProvider.Setup(p => p.ActionDescriptors)
                .Returns(new ActionDescriptorCollection(actionDescriptors, 1));

            var service = CreateControllerDiscoveryService();

            // Act
            var tools = service.DiscoverTools();

            // Assert
            Assert.Single(tools);
            var tool = tools.First();
            Assert.NotNull(tool.InputSchema);
            Assert.NotNull(tool.InputSchema.Properties);
            
            // Verify that the complex object parameter is detected as query source
            // Before the fix, this would be incorrectly classified as "body"
            var requestParam = tool.InputSchema.Properties.FirstOrDefault(p => p.Key == "request");
            Assert.True(requestParam.Key != null, "Request parameter should be present");
            Assert.Equal("query", requestParam.Value.Source);
        }

        [Fact]
        public void DiscoverTools_WithInheritedProperties_IncludesBaseClassProperties()
        {
            // Arrange - This tests Level 2 fix: inheritance chain walking
            var actionDescriptors = CreateTestActionDescriptorsWithInheritedModel();
            _mockActionDescriptorProvider.Setup(p => p.ActionDescriptors)
                .Returns(new ActionDescriptorCollection(actionDescriptors, 1));

            var service = CreateControllerDiscoveryService();

            // Act
            var tools = service.DiscoverTools();

            // Assert
            Assert.Single(tools);
            var tool = tools.First();
            Assert.NotNull(tool.InputSchema);
            Assert.NotNull(tool.InputSchema.Properties);
            
            // Verify the main parameter exists
            var requestParam = tool.InputSchema.Properties.FirstOrDefault(p => p.Key == "request");
            Assert.True(requestParam.Key != null, "Request parameter should be present");
            Assert.NotNull(requestParam.Value.Properties);
            
            // Verify inherited properties from BaseRequest are included
            Assert.True(requestParam.Value.Properties.ContainsKey("Provider"), "Provider property from base class should be included");
            Assert.True(requestParam.Value.Properties.ContainsKey("ModelName"), "ModelName property from base class should be included");
            Assert.True(requestParam.Value.Properties.ContainsKey("PromptVersion"), "PromptVersion property from base class should be included");
            
            // Verify derived properties are included
            Assert.True(requestParam.Value.Properties.ContainsKey("PromptType"), "PromptType property from derived class should be included");
            Assert.True(requestParam.Value.Properties.ContainsKey("TenantId"), "TenantId property from derived class should be included");
            
            // Verify required properties include both base and derived properties
            Assert.Contains("Provider", requestParam.Value.Required);
            Assert.Contains("ModelName", requestParam.Value.Required);
            Assert.Contains("PromptVersion", requestParam.Value.Required);
            Assert.Contains("PromptType", requestParam.Value.Required);
            Assert.Contains("TenantId", requestParam.Value.Required);
        }

        [Fact]
        public void DiscoverTools_WithFromQueryAndInheritance_BothFixesWorkTogether()
        {
            // Arrange - This tests both Level 1 and Level 2 fixes working together
            var actionDescriptors = CreateTestActionDescriptorsWithFromQueryComplex();
            _mockActionDescriptorProvider.Setup(p => p.ActionDescriptors)
                .Returns(new ActionDescriptorCollection(actionDescriptors, 1));

            var service = CreateControllerDiscoveryService();

            // Act
            var tools = service.DiscoverTools();

            // Assert
            Assert.Single(tools);
            var tool = tools.First();
            
            // Level 1 fix: Complex object should be detected as query source
            var requestParam = tool.InputSchema.Properties.FirstOrDefault(p => p.Key == "request");
            Assert.True(requestParam.Key != null);
            Assert.Equal("query", requestParam.Value.Source);
            
            // Level 2 fix: Should include inherited properties
            Assert.NotNull(requestParam.Value.Properties);
            Assert.True(requestParam.Value.Properties.ContainsKey("Provider"), "Should include base class properties");
            Assert.True(requestParam.Value.Properties.ContainsKey("PromptType"), "Should include derived class properties");
        }

        [Fact]
        public void ExtractRouteParameters_WithOptionalParameter_ShouldRemoveQuestionMark()
        {
            // Arrange
            var actionDescriptors = CreateTestActionDescriptorsWithOptionalRouteParameter();
            _mockActionDescriptorProvider.Setup(p => p.ActionDescriptors)
                .Returns(new ActionDescriptorCollection(actionDescriptors, 1));

            // Mock the type schema generator for integer types
            var intSchema = new McpSchema { Type = "integer" };
            _mockTypeSchemaGenerator.Setup(g => g.GenerateSchema(It.IsAny<Type>())).Returns(intSchema);

            var service = CreateControllerDiscoveryService();

            // Act
            var tools = service.DiscoverTools();

            // Assert
            Assert.Single(tools);
            var tool = tools.First();
            Assert.NotNull(tool.InputSchema);
            Assert.NotNull(tool.InputSchema.Properties);
            
            // The key assertion: should have 'customerId' not 'customerId?'
            Assert.True(tool.InputSchema.Properties.ContainsKey("customerId"), "Should contain 'customerId' parameter");
            Assert.False(tool.InputSchema.Properties.ContainsKey("customerId?"), "Should NOT contain 'customerId?' parameter with question mark");
            
            // Verify the parameter is properly configured
            var customerIdParam = tool.InputSchema.Properties["customerId"];
            Assert.Equal("integer", customerIdParam.Type);
            Assert.Equal("route", customerIdParam.Source);
        }

        [Fact]
        public void ExtractRouteParameters_WithMultipleOptionalParameters_ShouldRemoveAllQuestionMarks()
        {
            // Arrange
            var actionDescriptors = CreateTestActionDescriptorsWithMultipleOptionalRouteParameters();
            _mockActionDescriptorProvider.Setup(p => p.ActionDescriptors)
                .Returns(new ActionDescriptorCollection(actionDescriptors, 1));

            // Mock the type schema generator for integer types
            var intSchema = new McpSchema { Type = "integer" };
            _mockTypeSchemaGenerator.Setup(g => g.GenerateSchema(It.IsAny<Type>())).Returns(intSchema);

            var service = CreateControllerDiscoveryService();

            // Act
            var tools = service.DiscoverTools();

            // Assert
            Assert.Single(tools);
            var tool = tools.First();
            Assert.NotNull(tool.InputSchema);
            Assert.NotNull(tool.InputSchema.Properties);
            
            // Should have clean parameter names
            Assert.True(tool.InputSchema.Properties.ContainsKey("tenantId"), "Should contain 'tenantId' parameter");
            Assert.True(tool.InputSchema.Properties.ContainsKey("customerId"), "Should contain 'customerId' parameter");
            
            // Should NOT have question marks
            Assert.False(tool.InputSchema.Properties.ContainsKey("tenantId?"), "Should NOT contain 'tenantId?' with question mark");
            Assert.False(tool.InputSchema.Properties.ContainsKey("customerId?"), "Should NOT contain 'customerId?' with question mark");
        }

        [Fact]
        public void ExtractRouteParameters_WithOptionalParameterAndConstraints_ShouldHandleBoth()
        {
            // Arrange
            var actionDescriptors = CreateTestActionDescriptorsWithOptionalConstrainedRouteParameter();
            _mockActionDescriptorProvider.Setup(p => p.ActionDescriptors)
                .Returns(new ActionDescriptorCollection(actionDescriptors, 1));

            // Mock the type schema generator for integer types
            var intSchema = new McpSchema { Type = "integer" };
            _mockTypeSchemaGenerator.Setup(g => g.GenerateSchema(It.IsAny<Type>())).Returns(intSchema);

            var service = CreateControllerDiscoveryService();

            // Act
            var tools = service.DiscoverTools();

            // Assert
            Assert.Single(tools);
            var tool = tools.First();
            Assert.NotNull(tool.InputSchema);
            Assert.NotNull(tool.InputSchema.Properties);
            
            // Should extract clean parameter name even with constraints
            Assert.True(tool.InputSchema.Properties.ContainsKey("id"), "Should contain 'id' parameter");
            Assert.False(tool.InputSchema.Properties.ContainsKey("id?"), "Should NOT contain 'id?' with question mark");
            
            var idParam = tool.InputSchema.Properties["id"];
            Assert.Equal("integer", idParam.Type);
            Assert.Equal("route", idParam.Source);
        }

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

        private List<ControllerActionDescriptor> CreateTestActionDescriptors()
        {
            var controllerType = typeof(TestController);
            var methodInfo = controllerType.GetMethod(nameof(TestController.GetUser));
            
            return new List<ControllerActionDescriptor>
            {
                new ControllerActionDescriptor
                {
                    ControllerName = "TestController",
                    ActionName = "GetUser",
                    ControllerTypeInfo = controllerType.GetTypeInfo(),
                    MethodInfo = methodInfo!,
                    Parameters = new List<ParameterDescriptor>
                    {
                        (ParameterDescriptor)new ControllerParameterDescriptor
                        {
                            Name = "id",
                            ParameterType = typeof(int)
                        }
                    },
                    AttributeRouteInfo = new Microsoft.AspNetCore.Mvc.Routing.AttributeRouteInfo
                    {
                        Template = "api/test/{id}"
                    }
                }
            };
        }

        private List<ControllerActionDescriptor> CreateTestActionDescriptorsWithExcludeAttribute()
        {
            var controllerType = typeof(ExcludedController);
            var methodInfo = controllerType.GetMethod(nameof(ExcludedController.GetData));
            
            return new List<ControllerActionDescriptor>
            {
                new ControllerActionDescriptor
                {
                    ControllerName = "ExcludedController",
                    ActionName = "GetData",
                    ControllerTypeInfo = controllerType.GetTypeInfo(),
                    MethodInfo = methodInfo!
                }
            };
        }

        private List<ControllerActionDescriptor> CreateTestActionDescriptorsWithMethodExclude()
        {
            var controllerType = typeof(TestController);
            var methodInfo = controllerType.GetMethod(nameof(TestController.ExcludedMethod));
            
            return new List<ControllerActionDescriptor>
            {
                new ControllerActionDescriptor
                {
                    ControllerName = "TestController",
                    ActionName = "ExcludedMethod",
                    ControllerTypeInfo = controllerType.GetTypeInfo(),
                    MethodInfo = methodInfo!
                }
            };
        }

        private List<ControllerActionDescriptor> CreateTestActionDescriptorsWithComplexParameters()
        {
            var controllerType = typeof(TestController);
            var methodInfo = controllerType.GetMethod(nameof(TestController.CreateUser));
            
            return new List<ControllerActionDescriptor>
            {
                new ControllerActionDescriptor
                {
                    ControllerName = "TestController",
                    ActionName = "CreateUser",
                    ControllerTypeInfo = controllerType.GetTypeInfo(),
                    MethodInfo = methodInfo!,
                    Parameters = new List<ParameterDescriptor>
                    {
                        (ParameterDescriptor)new ControllerParameterDescriptor
                        {
                            Name = "id",
                            ParameterType = typeof(int)
                        },
                        (ParameterDescriptor)new ControllerParameterDescriptor
                        {
                            Name = "name",
                            ParameterType = typeof(string)
                        }
                    }
                }
            };
        }

        private List<ControllerActionDescriptor> CreateTestActionDescriptorsWithBodyParameter()
        {
            var controllerType = typeof(TestController);
            var methodInfo = controllerType.GetMethod(nameof(TestController.PostUser));
            
            return new List<ControllerActionDescriptor>
            {
                new ControllerActionDescriptor
                {
                    ControllerName = "TestController",
                    ActionName = "PostUser",
                    ControllerTypeInfo = controllerType.GetTypeInfo(),
                    MethodInfo = methodInfo!,
                    Parameters = new List<ParameterDescriptor>
                    {
                        (ParameterDescriptor)new ControllerParameterDescriptor
                        {
                            Name = "model",
                            ParameterType = typeof(UserModel),
                            BindingInfo = new Microsoft.AspNetCore.Mvc.ModelBinding.BindingInfo
                            {
                                BindingSource = Microsoft.AspNetCore.Mvc.ModelBinding.BindingSource.Body
                            }
                        }
                    }
                }
            };
        }

        private List<ControllerActionDescriptor> CreateTestActionDescriptorsWithAsyncMethod()
        {
            var controllerType = typeof(TestController);
            var methodInfo = controllerType.GetMethod(nameof(TestController.GetUserAsync));
            
            return new List<ControllerActionDescriptor>
            {
                new ControllerActionDescriptor
                {
                    ControllerName = "TestController",
                    ActionName = "GetUserAsync",
                    ControllerTypeInfo = controllerType.GetTypeInfo(),
                    MethodInfo = methodInfo!,
                    Parameters = new List<ParameterDescriptor>()
                }
            };
        }

        private List<ControllerActionDescriptor> CreateTestActionDescriptorsWithFromQueryComplex()
        {
            var controllerType = typeof(TestController);
            var methodInfo = controllerType.GetMethod(nameof(TestController.GetWithComplexQuery));
            
            return new List<ControllerActionDescriptor>
            {
                new ControllerActionDescriptor
                {
                    ControllerName = "TestController",
                    ActionName = "GetWithComplexQuery",
                    ControllerTypeInfo = controllerType.GetTypeInfo(),
                    MethodInfo = methodInfo!,
                    Parameters = new List<ParameterDescriptor>
                    {
                        (ParameterDescriptor)new ControllerParameterDescriptor
                        {
                            Name = "request",
                            ParameterType = typeof(ExtendedRequest),
                            BindingInfo = new Microsoft.AspNetCore.Mvc.ModelBinding.BindingInfo
                            {
                                BindingSource = Microsoft.AspNetCore.Mvc.ModelBinding.BindingSource.Query
                            }
                        }
                    }
                }
            };
        }

        private List<ControllerActionDescriptor> CreateTestActionDescriptorsWithInheritedModel()
        {
            var controllerType = typeof(TestController);
            var methodInfo = controllerType.GetMethod(nameof(TestController.PostWithInheritedModel));
            
            return new List<ControllerActionDescriptor>
            {
                new ControllerActionDescriptor
                {
                    ControllerName = "TestController",
                    ActionName = "PostWithInheritedModel",
                    ControllerTypeInfo = controllerType.GetTypeInfo(),
                    MethodInfo = methodInfo!,
                    Parameters = new List<ParameterDescriptor>
                    {
                        (ParameterDescriptor)new ControllerParameterDescriptor
                        {
                            Name = "request",
                            ParameterType = typeof(ExtendedRequest),
                            BindingInfo = new Microsoft.AspNetCore.Mvc.ModelBinding.BindingInfo
                            {
                                BindingSource = Microsoft.AspNetCore.Mvc.ModelBinding.BindingSource.Body
                            }
                        }
                    }
                }
            };
        }

        private List<ControllerActionDescriptor> CreateTestActionDescriptorsWithOptionalRouteParameter()
        {
            var controllerType = typeof(TestController);
            var methodInfo = controllerType.GetMethod(nameof(TestController.GetCustomerWithOptional));
            
            return new List<ControllerActionDescriptor>
            {
                new ControllerActionDescriptor
                {
                    ControllerName = "TestController",
                    ActionName = "GetCustomerWithOptional",
                    ControllerTypeInfo = controllerType.GetTypeInfo(),
                    MethodInfo = methodInfo!,
                    Parameters = new List<ParameterDescriptor>
                    {
                        (ParameterDescriptor)new ControllerParameterDescriptor
                        {
                            Name = "customerId",
                            ParameterType = typeof(int?)
                        }
                    },
                    AttributeRouteInfo = new Microsoft.AspNetCore.Mvc.Routing.AttributeRouteInfo
                    {
                        Template = "api/customer/{customerId?}"
                    }
                }
            };
        }

        private List<ControllerActionDescriptor> CreateTestActionDescriptorsWithMultipleOptionalRouteParameters()
        {
            var controllerType = typeof(TestController);
            var methodInfo = controllerType.GetMethod(nameof(TestController.GetTenantCustomerWithOptional));
            
            return new List<ControllerActionDescriptor>
            {
                new ControllerActionDescriptor
                {
                    ControllerName = "TestController",
                    ActionName = "GetTenantCustomerWithOptional",
                    ControllerTypeInfo = controllerType.GetTypeInfo(),
                    MethodInfo = methodInfo!,
                    Parameters = new List<ParameterDescriptor>
                    {
                        (ParameterDescriptor)new ControllerParameterDescriptor
                        {
                            Name = "tenantId",
                            ParameterType = typeof(int?)
                        },
                        (ParameterDescriptor)new ControllerParameterDescriptor
                        {
                            Name = "customerId",
                            ParameterType = typeof(int?)
                        }
                    },
                    AttributeRouteInfo = new Microsoft.AspNetCore.Mvc.Routing.AttributeRouteInfo
                    {
                        Template = "api/tenant/{tenantId?}/customer/{customerId?}"
                    }
                }
            };
        }

        private List<ControllerActionDescriptor> CreateTestActionDescriptorsWithOptionalConstrainedRouteParameter()
        {
            var controllerType = typeof(TestController);
            var methodInfo = controllerType.GetMethod(nameof(TestController.GetWithOptionalConstraint));
            
            return new List<ControllerActionDescriptor>
            {
                new ControllerActionDescriptor
                {
                    ControllerName = "TestController",
                    ActionName = "GetWithOptionalConstraint",
                    ControllerTypeInfo = controllerType.GetTypeInfo(),
                    MethodInfo = methodInfo!,
                    Parameters = new List<ParameterDescriptor>
                    {
                        (ParameterDescriptor)new ControllerParameterDescriptor
                        {
                            Name = "id",
                            ParameterType = typeof(int?)
                        }
                    },
                    AttributeRouteInfo = new Microsoft.AspNetCore.Mvc.Routing.AttributeRouteInfo
                    {
                        Template = "api/items/{id:int?}"
                    }
                }
            };
        }
    }

    // Test controller classes
    public class TestController : ControllerBase
    {
        [HttpGet]
        public IActionResult GetUser(int id) => Ok($"User {id}");

        [HttpPost]
        public IActionResult CreateUser(int id, string name) => Ok();

        [HttpPost]
        public IActionResult PostUser([FromBody] UserModel model) => Ok();

        [HttpGet]
        public async Task<string> GetUserAsync() => await Task.FromResult("User");

        [MCPExclude]
        [HttpGet]
        public IActionResult ExcludedMethod() => Ok();

        // Test method for [FromQuery] complex object (Level 1 fix)
        [HttpGet]
        public IActionResult GetWithComplexQuery([FromQuery] ExtendedRequest request) => Ok();

        // Test method for inheritance chain walking (Level 2 fix)  
        [HttpPost]
        public IActionResult PostWithInheritedModel([FromBody] ExtendedRequest request) => Ok();

        // Test methods for optional route parameter parsing
        [HttpGet]
        public IActionResult GetCustomerWithOptional(int? customerId) => Ok($"Customer {customerId}");

        [HttpGet]
        public IActionResult GetTenantCustomerWithOptional(int? tenantId, int? customerId) => Ok($"Tenant {tenantId}, Customer {customerId}");

        [HttpGet]
        public IActionResult GetWithOptionalConstraint(int? id) => Ok($"Item {id}");
    }

    [MCPExclude]
    public class ExcludedController : ControllerBase
    {
        [HttpGet]
        public IActionResult GetData() => Ok();
    }

    public class UserModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    // Test models for inheritance chain testing (mirrors LlmProviderModelRequest hierarchy)
    public class BaseRequest
    {
        [Required]
        public string Provider { get; set; } = string.Empty;
        
        [Required]
        public string ModelName { get; set; } = string.Empty;
        
        [Required]
        public string PromptVersion { get; set; } = string.Empty;
    }

    public class ExtendedRequest : BaseRequest
    {
        [Required]
        public string PromptType { get; set; } = string.Empty;
        
        [Required]
        [Range(1, int.MaxValue)]
        public int TenantId { get; set; }
    }
}