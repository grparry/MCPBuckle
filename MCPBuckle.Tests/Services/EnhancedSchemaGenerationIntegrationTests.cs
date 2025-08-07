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
    /// Integration tests for enhanced schema generation capabilities added in MCPBuckle 1.5.2
    /// to align with MCPInvoke 1.4.0. These tests verify the new features work end-to-end.
    /// </summary>
    public class EnhancedSchemaGenerationIntegrationTests : IDisposable
    {
        private readonly ServiceProvider _serviceProvider;
        private readonly ControllerDiscoveryService _discoveryService;

        public EnhancedSchemaGenerationIntegrationTests()
        {
            var services = new ServiceCollection();
            
            // Configure the services exactly like the main test setup
            services.Configure<McpBuckleOptions>(options =>
            {
                options.IncludeXmlDocumentation = false; // Disable to avoid file dependencies
                options.IncludeControllerNameInToolName = true;
            });

            services.AddLogging();
            services.AddSingleton<XmlDocumentationService>();
            services.AddSingleton<TypeSchemaGenerator>();
            services.AddSingleton<ControllerDiscoveryService>();
            services.AddSingleton<IActionDescriptorCollectionProvider>(provider =>
            {
                // Create action descriptors for our test controllers
                var descriptors = new List<Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor>
                {
                    CreateActionDescriptor("TestRouteController", "GetById", "GET", "api/test/{id}", 
                        new[] { CreateParameter("id", typeof(int)) }),
                    
                    CreateActionDescriptor("TestComplexController", "ProcessRequest", "POST", "api/complex", 
                        new[] { CreateParameter("request", typeof(TestRequest)) }),
                        
                    CreateActionDescriptor("TestMultiParamController", "UpdateItem", "PUT", "api/items/{itemId}/category/{categoryId}", 
                        new[] { 
                            CreateParameter("itemId", typeof(int)),
                            CreateParameter("categoryId", typeof(string)),
                            CreateParameter("updateData", typeof(TestUpdateData))
                        })
                };

                return new TestActionDescriptorCollectionProvider(descriptors);
            });

            _serviceProvider = services.BuildServiceProvider();
            _discoveryService = _serviceProvider.GetRequiredService<ControllerDiscoveryService>();
        }

        [Fact]
        public void DiscoverTools_RouteParameterExtraction_WorksCorrectly()
        {
            // Act
            var tools = _discoveryService.DiscoverTools();
            
            // Assert
            Assert.NotEmpty(tools);
            
            // Find the route parameter tool
            var routeTool = tools.FirstOrDefault(t => t.Name.Contains("TestRouteController"));
            Assert.NotNull(routeTool);
            
            // Should have id parameter extracted from route
            Assert.True(routeTool.InputSchema.Properties.ContainsKey("id"));
            var idProperty = routeTool.InputSchema.Properties["id"];
            Assert.Equal("integer", idProperty.Type);
            
            // Verify route parameter is marked as required
            Assert.Contains("id", routeTool.InputSchema.Required);
        }

        [Fact]
        public void DiscoverTools_ComplexObjectHandling_GeneratesDetailedSchema()
        {
            // Act
            var tools = _discoveryService.DiscoverTools();
            
            // Assert
            Assert.NotEmpty(tools);
            
            // Find the complex object tool
            var complexTool = tools.FirstOrDefault(t => t.Name.Contains("TestComplexController"));
            Assert.NotNull(complexTool);
            
            // Should have request parameter
            Assert.True(complexTool.InputSchema.Properties.ContainsKey("request"));
            var requestProperty = complexTool.InputSchema.Properties["request"];
            Assert.Equal("object", requestProperty.Type);
            
            // Should have properties defined
            Assert.NotNull(requestProperty.Properties);
            Assert.True(requestProperty.Properties.Count > 0);
        }

        [Fact]
        public void DiscoverTools_MultipleParameterTypes_HandlesAllCorrectly()
        {
            // Act
            var tools = _discoveryService.DiscoverTools();
            
            // Assert
            Assert.NotEmpty(tools);
            
            // Find the multi-parameter tool
            var multiTool = tools.FirstOrDefault(t => t.Name.Contains("TestMultiParamController"));
            Assert.NotNull(multiTool);
            
            // Should have both route parameters and body parameter
            Assert.True(multiTool.InputSchema.Properties.ContainsKey("itemId"));
            Assert.True(multiTool.InputSchema.Properties.ContainsKey("categoryId"));
            Assert.True(multiTool.InputSchema.Properties.ContainsKey("updateData"));
            
            // Route parameters should be marked as required
            Assert.Contains("itemId", multiTool.InputSchema.Required);
            Assert.Contains("categoryId", multiTool.InputSchema.Required);
        }

        [Fact]
        public void DiscoverTools_EnhancedSchemaGeneration_MaintainsBackwardCompatibility()
        {
            // Verify that enhanced schema generation doesn't break existing functionality
            
            // Act
            var tools = _discoveryService.DiscoverTools();
            
            // Assert
            Assert.NotEmpty(tools);
            
            foreach (var tool in tools)
            {
                // All tools should have basic required properties
                Assert.NotNull(tool.Name);
                Assert.False(string.IsNullOrEmpty(tool.Name));
                Assert.NotNull(tool.Description);
                Assert.NotNull(tool.InputSchema);
                Assert.Equal("object", tool.InputSchema.Type);
                Assert.NotNull(tool.InputSchema.Properties);
                Assert.NotNull(tool.InputSchema.Required);
            }
        }

        [Fact]
        public void DiscoverTools_TypeMapping_WorksForAllBasicTypes()
        {
            // This test verifies that our type mapping helpers work correctly
            // by checking tools that have different parameter types
            
            // Act
            var tools = _discoveryService.DiscoverTools();
            
            // Assert
            Assert.NotEmpty(tools);
            
            // Find tool with int parameter
            var routeTool = tools.FirstOrDefault(t => t.Name.Contains("TestRouteController"));
            Assert.NotNull(routeTool);
            
            var idProperty = routeTool.InputSchema.Properties["id"];
            Assert.Equal("integer", idProperty.Type);
            
            // Find tool with string parameter
            var multiTool = tools.FirstOrDefault(t => t.Name.Contains("TestMultiParamController"));
            Assert.NotNull(multiTool);
            
            var categoryProperty = multiTool.InputSchema.Properties["categoryId"];
            Assert.Equal("string", categoryProperty.Type);
        }

        #region Helper Methods and Classes

        private static ControllerActionDescriptor CreateActionDescriptor(string controller, string action, string httpMethod, string route, ControllerParameterDescriptor[] parameters)
        {
            var descriptor = new ControllerActionDescriptor
            {
                ControllerName = controller,
                ActionName = action,
                AttributeRouteInfo = new Microsoft.AspNetCore.Mvc.Routing.AttributeRouteInfo { Template = route },
                Parameters = parameters.Cast<Microsoft.AspNetCore.Mvc.Abstractions.ParameterDescriptor>().ToList(),
                ControllerTypeInfo = typeof(TestController).GetTypeInfo(),
                MethodInfo = typeof(TestController).GetMethod("GetById") ?? typeof(TestController).GetMethod("DefaultAction")
            };

            return descriptor;
        }

        private static ControllerParameterDescriptor CreateParameter(string name, Type type)
        {
            return new ControllerParameterDescriptor
            {
                Name = name,
                ParameterType = type
            };
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
            
            public List<TestItem> Items { get; set; } = new();
        }

        public class TestUpdateData
        {
            public string Title { get; set; } = string.Empty;
            
            public decimal Price { get; set; }
            
            public TestCategory Category { get; set; } = new();
        }

        public class TestItem
        {
            public int Id { get; set; }
            
            public string Name { get; set; } = string.Empty;
        }

        public class TestCategory
        {
            public string Name { get; set; } = string.Empty;
            
            public string Code { get; set; } = string.Empty;
        }

        public enum TestStatus
        {
            Active,
            Inactive,
            Pending
        }

        [ApiController]
        public class TestController : ControllerBase
        {
            [HttpGet("api/test/{id}")]
            public IActionResult GetById(int id) => Ok();
            
            [HttpPost("api/complex")]
            public IActionResult ProcessRequest(TestRequest request) => Ok();
            
            [HttpPut("api/items/{itemId}/category/{categoryId}")]
            public IActionResult UpdateItem(int itemId, string categoryId, TestUpdateData updateData) => Ok();
            
            public IActionResult DefaultAction() => Ok();
        }

        #endregion
    }
}