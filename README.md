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
});
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

5. Your API will now serve MCP JSON at `/.well-known/mcp-context`.

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
