# MCPBuckle

MCPBuckle is a comprehensive .NET library that generates [Model Context Protocol (MCP)](https://github.com/microsoft/mcp) JSON from ASP.NET Core API metadata with full MCPInvoke compatibility. It works alongside Swashbuckle to enable AI agent discovery and execution of your APIs.

## Overview

MCPBuckle reads the same metadata as Swashbuckle (XML comments, controller metadata, type annotations) but generates MCP JSON at multiple discovery endpoints. This allows AI agents to discover and understand your API's capabilities with sophisticated schema generation and parameter source detection.

MCPBuckle is designed to be a **complementary tool** to Swashbuckle, not a replacement. It enables your API to participate in AI agent discovery and execution with minimal developer friction.

## Features

- **Full MCPInvoke 2.0.0+ Compatibility** - Seamless integration with MCP execution systems
- **Advanced Schema Generation** - Route parameter extraction, complex object schemas, parameter source detection
- **Multiple Discovery Endpoints** - Traditional `.well-known/mcp-context` and modern `/api/discovery/tools`
- **Enhanced Parameter Handling** - Automatic source detection (route/body/query/header) with annotations
- **Complex Type Support** - Recursive object schemas, enum handling, array types, and nested objects
- **XML Documentation Integration** - Extracts rich documentation from XML comments
- **Granular Control** - Include/exclude controllers and actions with flexible configuration
- **Side-by-side with Swashbuckle** - Works seamlessly alongside existing OpenAPI tooling

## What's New in 2.1.0

### 🔧 Fixed - Claude Code CLI Compatibility

- **Critical Fix: Optional Route Parameter Parsing** - Resolved Claude Code CLI compatibility issues
  - Fixed regex pattern to properly strip `?` from parameter names like `{customerId?}`
  - Parameter names now correctly parse as `customerId` instead of `customerId?`
  - Resolves Claude Code CLI error: `Property keys should match pattern '^[a-zA-Z0-9_.-]{1,64}$'`

- **Enhanced Type Mapping: Nullable Type Support** - Added support for nullable types in route parameters
  - Nullable types like `int?`, `bool?` now correctly unwrap to their underlying type
  - `int?` now properly maps to `"integer"` instead of `"object"` in JSON schema

### ✅ Quality Assurance
- **3 New TDD Tests Added** - Comprehensive test coverage for optional route parameter fixes
- **128/128 Tests Passing** - All tests continue to pass, ensuring backward compatibility
- **End-to-End Validation** - All 247 MCP tools now compatible with Claude Code CLI

## What's New in 2.0.0

### 🚀 Major Enhancement - Enhanced Parameter Source Detection

- **Advanced Runtime Parameter Binding** - Complete rewrite of parameter source detection system
  - Enhanced route parameter detection using ASP.NET Core reflection patterns
  - Comprehensive parameter source analysis for route, query, body, and header parameters
  - Schema-aware parameter binding that mirrors ASP.NET Core's parameter binding logic exactly
  - Runtime parameter source detection with full metadata preservation

- **MCPInvoke 2.0.0 Integration** - Full compatibility with enhanced parameter binding
  - Seamless integration with MCPInvoke 2.0.0's enhanced parameter binding service
  - Coordinated v2.0.0 release for complete MCP tool discovery and execution
  - Enhanced schema generation with comprehensive route template analysis
  - Advanced parameter validation and intelligent error handling

### 🔧 Enhanced Architecture
- **Comprehensive Route Template Analysis** - Advanced route parameter extraction and validation
- **Generic Test Pattern Framework** - OSS-ready test patterns for any web API domain
- **Multi-Framework Support** - Complete targeting for net6.0, net7.0, net8.0, net9.0
- **Enhanced Documentation** - Updated descriptions highlighting v2.0 capabilities

### ✅ Quality Assurance
- **125/125 Tests Passing** - Complete test coverage maintained across all frameworks
- **Enhanced Parameter Detection Tests** - Comprehensive validation for new parameter binding logic
- **Generic Web API Patterns** - OSS-ready test patterns replacing proprietary content
- **Backward Compatibility** - All existing functionality preserved and enhanced

## What's New in 1.7.0 (Superseded by 2.0.0)

### 🚀 Critical Fixes for Complex Parameter Handling

- **Fixed: [FromQuery] Complex Object Detection** - Resolved critical issue where complex objects with `[FromQuery]` attribute were incorrectly classified as "body" parameters
  - Complex objects with `[FromQuery]` now properly return "query" source instead of "body"
  - Fixes MCP tool generation for complex query parameter types like `PromptRequest` and `TenantPromptSettingsRequest`
  - Essential for LLM prompt management APIs with inheritance-based parameter structures

- **Fixed: Inheritance Chain Property Walking** - Resolved issue where base class properties were missing from MCP tool definitions
  - Added comprehensive inheritance chain walking that includes all base class properties
  - Base class properties (like `Provider`, `ModelName`, `PromptVersion`) now appear in MCP tool schemas
  - Required attribute detection now works across inheritance hierarchies
  - Critical for APIs using inheritance-based parameter models

### 🔧 Enhanced Schema Generation
- **Complete Parameter Schemas** - Both fixes work together to provide comprehensive parameter expansion
- **Inheritance-Aware Processing** - Full property walking across inheritance chains
- **Correct Source Annotations** - Proper parameter source detection for complex inherited objects
- **Backward Compatibility** - All existing functionality preserved and enhanced

### ✅ Comprehensive Testing
- **3 New Targeted Tests** - Specific validation for both fixes working individually and together
- **100% Test Pass Rate** - All new and existing tests pass, ensuring stability
- **Real-World Test Models** - Test cases mirror actual usage patterns with inheritance hierarchies

## What's New in 1.6.0

### 🚀 Major New Features
- **MCPInvoke 2.0.0+ Compatibility** - Full alignment with modern MCP execution systems
- **New `/api/discovery/tools` Endpoint** - Enhanced tool discovery endpoint for tool management integration
- **Advanced Schema Generation** - Route parameter extraction from templates, complex object introspection
- **Parameter Source Detection** - Automatic detection and annotation of parameter sources (route/body/query/header)
- **Enhanced Annotations System** - Rich metadata annotations for precise parameter handling

### 📈 Schema Generation Improvements
- Route parameter extraction from route templates (e.g., `api/users/{userId}/orders/{orderId}`)
- Complex object schema generation with recursive property mapping
- Parameter source inference with explicit binding source detection
- Enhanced enum and array handling with detailed schema definitions
- Annotations support for MCPInvoke compatibility

### ✅ Quality Assurance
- **107 comprehensive tests** covering all functionality
- **Zero compiler warnings** across all target frameworks
- **Multi-framework support** - net6.0, net7.0, net8.0, net9.0
- **Backward compatibility** - All existing functionality preserved

### 🔧 Developer Experience
- Comprehensive test suite covering advanced schema generation
- Enhanced error handling and fallback mechanisms
- Improved logging and diagnostics
- Better integration with ASP.NET Core infrastructure types

## What's New in 1.5.1

### Bug Fixes and Improvements
- Fixed AmbiguousMatchException in XmlDocumentationService when retrieving overloaded methods
- Fixed enum schema generation to properly handle JsonStringEnumConverter
- Fixed dictionary type detection to prevent misidentification as arrays
- Added circular reference detection in TypeSchemaGenerator to prevent stack overflow
- Updated default values for better consistency (Description now defaults to empty string)

### Testing Enhancements
- Added comprehensive unit test suite covering all core functionality
- Added integration tests using TestServer for testing MCP endpoint behavior
- Added specific tests for circular reference handling in schema generation

## Installation

Install the MCPBuckle NuGet package:

```
dotnet add package MCPBuckle
```

## Usage

1. Enable XML documentation in your project file:

```xml
<PropertyGroup>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
</PropertyGroup>
```

2. Register MCPBuckle services in your `Program.cs` or `Startup.cs`:

```csharp
// Basic configuration
builder.Services.AddMcpBuckle();

// Or with custom options
builder.Services.AddMcpBuckle(options =>
{
    // Customize the route prefix
    options.RoutePrefix = "/.well-known/mcp-context";
    
    // Include controller name in tool name
    options.IncludeControllerNameInToolName = true;
    
    // Include or exclude specific controllers
    options.IncludeControllers = new List<string> { "Todo", "Products" };
    // options.ExcludeControllers = new List<string> { "Internal" };
    
    // Custom tool naming
    options.CustomToolNameFactory = (controllerName, actionName) => 
        $"{controllerName.ToLowerInvariant()}.{actionName.ToLowerInvariant()}";
    
    // Add custom metadata
    options.Metadata.Add("contact", new Dictionary<string, string>
    {
        { "name", "API Team" },
        { "url", "https://example.com/contact" }
    });
    
    // MCP info section configuration
    options.SchemaVersion = "1.0";
    options.ServerTitle = "My API";
    options.ServerDescription = "A comprehensive API for managing resources";
});

// Controller discovery with assembly scanning
builder.Services.AddMcpControllerDiscovery(
    options => {
        // Configure options as above
        options.IncludeControllerNameInToolName = true;
    },
    // Specify assemblies to scan for controllers
    typeof(Program).Assembly,
    typeof(ExternalLibrary.Controller).Assembly
);
```

3. Add the MCPBuckle middleware to your application pipeline:

```csharp
// Use MCPBuckle middleware to expose MCP context endpoints
app.UseMcpBuckle();

// Or with a custom path
app.UseMcpBuckle("/api/mcp");
```

4. Document your controllers and actions with XML comments:

```csharp
/// <summary>
/// Gets all todo items for a specific user.
/// </summary>
/// <param name="userId">The user ID to get todos for</param>
/// <param name="status">Optional status filter</param>
/// <returns>A list of todo items matching the criteria.</returns>
[HttpGet("api/users/{userId}/todos")]
public ActionResult<IEnumerable<TodoItem>> GetUserTodos(int userId, [FromQuery] string? status = null)
{
    // MCPBuckle will automatically detect:
    // - userId as route parameter
    // - status as query parameter
    // - Generate appropriate schema with source annotations
}

/// <summary>
/// Creates a new todo item.
/// </summary>
/// <param name="request">The todo item creation request</param>
/// <returns>The created todo item.</returns>
[HttpPost("api/todos")]
public ActionResult<TodoItem> CreateTodo([FromBody] CreateTodoRequest request)
{
    // MCPBuckle will generate complex object schema for CreateTodoRequest
    // with detailed property mapping and validation requirements
}
```

5. Use the `MCPExcludeAttribute` to exclude specific controllers or actions:

```csharp
// Exclude an entire controller
[MCPExclude("Internal use only")]
public class InternalController : ControllerBase
{
    // This entire controller will be excluded from MCP discovery
}

// Exclude a specific action method
public class ProductsController : ControllerBase
{
    // This method will be included in MCP discovery
    [HttpGet]
    public ActionResult<IEnumerable<Product>> GetAll() { /* ... */ }
    
    // This method will be excluded from MCP discovery
    [HttpPost]
    [MCPExclude("Requires special permissions")]
    public ActionResult<Product> Create(Product product) { /* ... */ }
}
```

6. Your API will now serve MCP JSON at multiple endpoints:
   - `/.well-known/mcp-context` (traditional endpoint)
   - `/api/discovery/tools` (MCPInvoke 2.0.0+ compatible endpoint)

## Enhanced Schema Generation (Enhanced in 2.0.0)

MCPBuckle now generates sophisticated schemas that fully support MCPInvoke 2.0.0+ execution:

```csharp
[HttpPut("api/orders/{orderId}/items/{itemId}")]
public ActionResult<OrderItem> UpdateOrderItem(
    int orderId,                    // Auto-detected as route parameter
    Guid itemId,                    // Auto-detected as route parameter  
    [FromQuery] bool notify,        // Explicitly marked as query parameter
    [FromHeader] string apiKey,     // Explicitly marked as header parameter
    [FromBody] UpdateItemRequest request)  // Complex object with full schema
{
    // MCPBuckle generates:
    // {
    //   "type": "object",
    //   "properties": {
    //     "orderId": {
    //       "type": "integer",
    //       "description": "Route parameter orderId",
    //       "annotations": { "source": "route" }
    //     },
    //     "itemId": {
    //       "type": "string", 
    //       "description": "Route parameter itemId",
    //       "annotations": { "source": "route" }
    //     },
    //     "notify": {
    //       "type": "boolean",
    //       "annotations": { "source": "query" }
    //     },
    //     "apiKey": {
    //       "type": "string",
    //       "annotations": { "source": "header" }
    //     },
    //     "request": {
    //       "type": "object",
    //       "description": "Complex object of type UpdateItemRequest",
    //       "annotations": {
    //         "source": "body",
    //         "properties": { /* detailed property schemas */ }
    //       }
    //     }
    //   },
    //   "required": ["orderId", "itemId", "request"]
    // }
}
```

## Side-by-Side with Swashbuckle

MCPBuckle is designed to work alongside Swashbuckle. You can use both in the same project:

```csharp
// Configure Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "My API", Version = "v1" });
    
    // Enable XML comments
    var xmlFilename = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));
});

// Configure MCPBuckle
builder.Services.AddMcpBuckle();

// In the application pipeline:
app.UseSwagger();
app.UseSwaggerUI();
app.UseMcpBuckle();
```

This configuration will serve:
- **Swagger JSON** at `/swagger/v1/swagger.json`
- **Swagger UI** at `/swagger`
- **MCP JSON** at `/.well-known/mcp-context` (traditional)
- **MCP Tools** at `/api/discovery/tools` (MCPInvoke compatible)

## MCPInvoke Integration

MCPBuckle 2.0.0 provides full compatibility with MCPInvoke 2.0.0+ execution systems:

```csharp
// MCPInvoke can now discover and execute your APIs with:
// - Precise parameter binding using source annotations
// - Complex object validation using detailed schemas  
// - Route parameter extraction from URL templates
// - Comprehensive error handling and type conversion

// Example: MCPInvoke execution of the above endpoint
// POST /api/discovery/tools -> discovers UpdateOrderItem tool
// MCPInvoke executes: UpdateOrderItem(orderId: 123, itemId: "abc-def", ...)
// with proper parameter binding based on source annotations
```

## Framework Support

- **.NET 6.0** - Long Term Support
- **.NET 7.0** - Standard Term Support  
- **.NET 8.0** - Long Term Support
- **.NET 9.0** - Standard Term Support

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

### Development

```bash
# Clone the repository
git clone https://github.com/grparry/MCPBuckle.git

# Build the project
dotnet build

# Run tests
dotnet test

# Run specific test categories
dotnet test --filter "FullyQualifiedName~McpDiscoveryEndpointTests"
```

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Roadmap

- **Future Releases**:
  - OpenTelemetry integration for observability
  - Enhanced caching mechanisms
  - GraphQL endpoint support
  - Advanced authentication/authorization patterns