using System;
using System.Collections.Generic;
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

        private ControllerDiscoveryService CreateControllerDiscoveryService(McpBuckleOptions options = null)
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
                    MethodInfo = methodInfo,
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
                    MethodInfo = methodInfo
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
                    MethodInfo = methodInfo
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
                    MethodInfo = methodInfo,
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
                    MethodInfo = methodInfo,
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
                    MethodInfo = methodInfo,
                    Parameters = new List<ParameterDescriptor>()
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
}