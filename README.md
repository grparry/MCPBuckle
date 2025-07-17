# MCPBuckle

MCPBuckle is a lightweight .NET library that generates [Model Context Protocol (MCP)](https://github.com/microsoft/mcp) JSON from ASP.NET Core API metadata. It works alongside Swashbuckle to enable AI agent discovery of your APIs.

## Overview

MCPBuckle reads the same metadata as Swashbuckle (XML comments, controller metadata, type annotations) but generates MCP JSON at the `.well-known/mcp-context` endpoint. This allows AI agents to discover and understand your API's capabilities.

MCPBuckle is designed to be a **complementary tool** to Swashbuckle, not a replacement. It enables your API to participate in AI agent discovery with minimal developer friction.

## Features

- Generates MCP JSON from ASP.NET Core controllers and actions
- Extracts documentation from XML comments
- Serves MCP context at the `.well-known/mcp-context` endpoint
- Works alongside Swashbuckle without conflicts
- Supports configuration options for customization
- Enhanced schema generation for complex types
- Support for required fields, enums, arrays, and nested objects
- Customizable tool naming and metadata

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

## What's New in 1.5.0

### Enhanced Schema Generation
- Added support for outputSchema generation to expose the return type of API endpoints
- Improved enum handling with support for both string and integer-based enums
- Added proper enum value lists to schema definitions

### Parameter Binding Improvements
- Fixed parameter binding for complex types to ensure better compatibility with MCPInvoke
- Preserved parameter names for body parameters to ensure correct binding

### Type Discovery and Reflection
- Enhanced output type discovery with support for Task<T>, ValueTask<T>, and ActionResult<T>
- Added collection type detection and proper schema generation for array responses
- Improved null/void return type handling

## What's New in 1.4.0

### MCP Specification Compliance Improvements
- Added proper `McpInfo` class to support the info section as required by the MCP specification
- Updated `McpContext` model to include the enhanced info section
- Added schema version, server title, and description properties to configuration options

### Granular Control Over Exposed Endpoints
- Added new `MCPExcludeAttribute` to exclude specific controllers or actions from MCP tool discovery
- Enhanced controller discovery with support for selectively exposing endpoints
- Added ability to prevent overwhelming AI clients with too many tools

### Improved Configuration Options
- Added new extension methods (`ControllerDiscoveryExtensions`) for easier MCPBuckle configuration
- Enhanced `McpBuckleOptions` with additional configuration properties
- Added support for scanning specific assemblies for controllers

### Code Quality Improvements
- Fixed compiler warnings related to nullable reference types
- Improved XML documentation
- Enhanced null handling for better reliability

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
    
    // New in 1.4.0: MCP info section configuration
    options.SchemaVersion = "1.0";
    options.ServerTitle = "My API";
    options.ServerDescription = "A comprehensive API for managing resources";
});

// New in 1.4.0: Controller discovery with assembly scanning
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
// Use MCPBuckle middleware to expose the MCP context at /.well-known/mcp-context
app.UseMcpBuckle();

// Or with a custom path
app.UseMcpBuckle("/api/mcp");
```

4. Document your controllers and actions with XML comments:

```csharp
/// <summary>
/// Gets all todo items.
/// </summary>
/// <returns>A list of all todo items.</returns>
[HttpGet]
public ActionResult<IEnumerable<TodoItem>> GetAll()
{
    // Implementation
}
```

5. Use the new `MCPExcludeAttribute` to exclude specific controllers or actions from MCP tool discovery (new in 1.4.0):

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

6. Your API will now serve MCP JSON at `/.well-known/mcp-context`.

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
- Swagger JSON at `/swagger/v1/swagger.json`
- Swagger UI at `/swagger`
- MCP JSON at `/.well-known/mcp-context`

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the MIT License - see the LICENSE file for details.
