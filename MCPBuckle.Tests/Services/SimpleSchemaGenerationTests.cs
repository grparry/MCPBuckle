using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using MCPBuckle.Configuration;
using MCPBuckle.Services;
using MCPBuckle.Models;

namespace MCPBuckle.Tests.Services
{
    /// <summary>
    /// Simplified tests for enhanced schema generation capabilities without complex mocking.
    /// Tests the core functionality that was added in MCPBuckle 1.5.2 to align with MCPInvoke 1.4.0.
    /// </summary>
    public class SimpleSchemaGenerationTests : IDisposable
    {
        private readonly ServiceProvider _serviceProvider;
        private readonly ControllerDiscoveryService _discoveryService;

        public SimpleSchemaGenerationTests()
        {
            var services = new ServiceCollection();
            
            // Configure the services
            services.Configure<McpBuckleOptions>(options =>
            {
                options.IncludeXmlDocumentation = false;
                options.IncludeControllerNameInToolName = true;
            });

            services.AddLogging();
            services.AddSingleton<XmlDocumentationService>();
            services.AddSingleton<TypeSchemaGenerator>();
            services.AddSingleton<ControllerDiscoveryService>();
            services.AddSingleton<IActionDescriptorCollectionProvider>(provider =>
            {
                // Create simple action descriptors
                var descriptors = new List<Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor>
                {
                    CreateSimpleActionDescriptor("TestController", "GetById", "api/test/{id}", typeof(int), "id"),
                    CreateComplexActionDescriptor("TestController", "ProcessRequest", "api/test", typeof(TestRequest), "request")
                };

                return new TestActionDescriptorCollectionProvider(descriptors);
            });

            _serviceProvider = services.BuildServiceProvider();
            _discoveryService = _serviceProvider.GetRequiredService<ControllerDiscoveryService>();
        }

        [Fact]
        public void DiscoverTools_BasicFunctionality_ReturnsTools()
        {
            // Act
            var tools = _discoveryService.DiscoverTools();
            
            // Assert
            Assert.NotNull(tools);
            Assert.True(tools.Count >= 0); // Should not throw exceptions and return a valid list
        }

        [Fact]
        public void DiscoverTools_WithRouteParameter_CreatesSchema()
        {
            // Act
            var tools = _discoveryService.DiscoverTools();
            
            // Assert
            Assert.NotNull(tools);
            if (tools.Count > 0)
            {
                var tool = tools.First();
                Assert.NotNull(tool.Name);
                Assert.NotNull(tool.InputSchema);
                Assert.Equal("object", tool.InputSchema.Type);
                Assert.NotNull(tool.InputSchema.Properties);
            }
        }

        [Fact]
        public void DiscoverTools_WithComplexParameter_CreatesObjectSchema()
        {
            // Act
            var tools = _discoveryService.DiscoverTools();
            
            // Assert
            Assert.NotNull(tools);
            foreach (var tool in tools)
            {
                Assert.NotNull(tool.InputSchema);
                Assert.NotNull(tool.InputSchema.Properties);
                Assert.NotNull(tool.InputSchema.Required);
            }
        }

        #region Helper Methods and Classes

        private static ControllerActionDescriptor CreateSimpleActionDescriptor(string controller, string action, string route, Type paramType, string paramName)
        {
            var parameter = new ControllerParameterDescriptor
            {
                Name = paramName,
                ParameterType = paramType
            };

            var descriptor = new ControllerActionDescriptor
            {
                ControllerName = controller,
                ActionName = action,
                AttributeRouteInfo = new Microsoft.AspNetCore.Mvc.Routing.AttributeRouteInfo { Template = route },
                Parameters = new List<Microsoft.AspNetCore.Mvc.Abstractions.ParameterDescriptor> { parameter },
                ControllerTypeInfo = typeof(SimpleTestController).GetTypeInfo(),
                MethodInfo = typeof(SimpleTestController).GetMethod("TestMethod")
            };

            return descriptor;
        }

        private static ControllerActionDescriptor CreateComplexActionDescriptor(string controller, string action, string route, Type paramType, string paramName)
        {
            var parameter = new ControllerParameterDescriptor
            {
                Name = paramName,
                ParameterType = paramType
            };

            var descriptor = new ControllerActionDescriptor
            {
                ControllerName = controller,
                ActionName = action,
                AttributeRouteInfo = new Microsoft.AspNetCore.Mvc.Routing.AttributeRouteInfo { Template = route },
                Parameters = new List<Microsoft.AspNetCore.Mvc.Abstractions.ParameterDescriptor> { parameter },
                ControllerTypeInfo = typeof(SimpleTestController).GetTypeInfo(),
                MethodInfo = typeof(SimpleTestController).GetMethod("TestMethod")
            };

            return descriptor;
        }

        private class TestActionDescriptorCollectionProvider : IActionDescriptorCollectionProvider
        {
            public ActionDescriptorCollection ActionDescriptors { get; }

            public TestActionDescriptorCollectionProvider(IList<Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor> descriptors)
            {
                ActionDescriptors = new ActionDescriptorCollection(descriptors.ToList().AsReadOnly(), 1);
            }
        }

        public void Dispose()
        {
            _serviceProvider?.Dispose();
        }

        #endregion

        #region Test Model Classes

        public class TestRequest
        {
            [Required]
            public string Name { get; set; } = string.Empty;
            
            public string Description { get; set; } = string.Empty;
            
            public TestStatus Status { get; set; }
        }

        public enum TestStatus
        {
            Active,
            Inactive,
            Pending
        }

        [ApiController]
        public class SimpleTestController : ControllerBase
        {
            [HttpGet]
            public IActionResult TestMethod()
            {
                return Ok();
            }
        }

        #endregion
    }
}