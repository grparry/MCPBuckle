# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.7.0] - 2025-01-11

### 🚀 Major Fixes for Complex Parameter Handling

- **Critical Fix: [FromQuery] Complex Object Detection** - Resolved issue where complex objects with `[FromQuery]` attribute were incorrectly classified as "body" parameters
  - Enhanced `DetectParameterSource()` method in `ControllerDiscoveryService` to explicitly check for `[FromQuery]` attribute
  - Complex objects with `[FromQuery]` are now properly classified as "query" source parameters
  - Fixes MCP tool generation for complex query parameter objects like `PromptRequest` and `TenantPromptSettingsRequest`

- **Critical Fix: Inheritance Chain Property Walking** - Resolved issue where base class properties were missing from MCP tool definitions
  - Added comprehensive `GetInheritanceChainProperties()` method that walks the full inheritance hierarchy
  - Updated `GenerateObjectProperties()` to use inheritance chain walking instead of immediate class properties only
  - Base class properties (like `Provider`, `ModelName`, `PromptVersion` from `LlmProviderModelRequest`) are now properly included
  - Fixes missing required properties in MCP tool schemas for inherited parameter types

### 🔧 Enhanced Parameter Processing

- **Improved Schema Generation** - Both fixes work together to provide complete parameter schemas
  - Complex `[FromQuery]` objects now expand to show all inherited properties as individual parameters
  - Required attribute detection works across inheritance chains
  - Parameter source annotations are correctly applied to inherited properties

### ✅ Quality Assurance

- **Comprehensive Test Coverage** - Added 3 new targeted tests to validate the fixes:
  - `DiscoverTools_WithFromQueryComplexObject_DetectsQuerySource` - Validates Level 1 fix for `[FromQuery]` detection
  - `DiscoverTools_WithInheritedProperties_IncludesBaseClassProperties` - Validates Level 2 fix for inheritance chain walking
  - `DiscoverTools_WithFromQueryAndInheritance_BothFixesWorkTogether` - Validates both fixes working in combination
- **Test Models Added** - Added `BaseRequest` and `ExtendedRequest` test models that mirror real-world inheritance scenarios
- **100% Test Pass Rate** - All existing tests continue to pass, ensuring backward compatibility

### Impact

These fixes specifically address MCP tool generation issues with inheritance-based parameter types commonly used in LLM prompt management systems. Before this version, MCP tools would show generic "Complex object of type PromptRequest" descriptions instead of exposing individual inherited properties. With version 1.7.0, MCP tools now correctly expose all inherited properties as separate, properly-typed parameters with correct source annotations.

## [1.6.0] - 2025-08-07

### 🚀 Major New Features
- **Full MCPInvoke 1.4.0+ Compatibility** - Complete alignment with modern MCP execution systems
  - Advanced schema generation with route parameter extraction from templates
  - Parameter source detection and annotation (route/body/query/header)
  - Enhanced annotations system for precise parameter handling
  - Comprehensive compatibility with MCPInvoke execution workflows

- **Advanced Schema Generation** - Revolutionary improvements to schema introspection
  - Route parameter extraction from route templates (e.g., `api/users/{userId}/orders/{orderId}`)
  - Complex object schema generation with recursive property mapping
  - Parameter source inference with explicit binding source detection
  - Enhanced enum and array handling with detailed schema definitions
  - Automatic detection of ASP.NET Core infrastructure types

- **Enhanced Discovery Endpoints** - Multiple endpoints for different MCP integration scenarios
  - Traditional `/.well-known/mcp-context` endpoint (backward compatibility)
  - Modern `/api/discovery/tools` endpoint (MCPInvoke 1.4.0+ tool management)
  - Consistent JSON format across both endpoints with rich metadata

### 📈 Schema Generation Improvements
- **Route Parameter Extraction** - Intelligent parsing of route templates to identify path parameters
- **Complex Object Introspection** - Deep analysis of object types with recursive property schemas
- **Parameter Source Detection** - Automatic classification of parameters by binding source
- **Type Mapping Enhancements** - Improved .NET type to JSON Schema type conversions
- **Error Handling** - Graceful fallback for schema generation failures

### ✅ Quality Assurance
- **107 Comprehensive Tests** - Extensive test coverage including:
  - 11 middleware discovery endpoint tests (100% passing)
  - 3 simple schema generation tests (100% passing)  
  - 10 original controller discovery tests (100% passing)
  - 8 integration tests (100% passing)
  - 75 existing functionality tests (100% passing)
- **Zero Compiler Warnings** - Clean codebase across all target frameworks
- **Multi-Framework Support** - Verified compatibility with .NET 6.0, 7.0, 8.0, 9.0
- **Backward Compatibility** - All existing functionality preserved and enhanced

### 🔧 Developer Experience
- **Enhanced Error Handling** - Improved error messages and fallback mechanisms
- **Comprehensive Logging** - Better diagnostics and debugging information
- **Simplified Testing** - New test utilities for schema generation validation
- **Documentation Improvements** - Updated README with 1.6.0 features and examples

### Fixed
- **Null Reference Warnings** - Resolved CS8600 and CS8604 compiler warnings in ControllerDiscoveryService
- **Type Safety** - Enhanced null handling in array element type detection

## [1.5.2] - 2025-08-07

### Added
- **MCPInvoke 1.4.0 Compatibility** - Added `/api/discovery/tools` endpoint to support MCPInvoke 1.4.0 schema discovery
  - New endpoint returns tools array in format expected by MCPInvoke tool management system
  - Maintains backward compatibility with existing `/.well-known/mcp-context` endpoint
  - Fixes "MCP Discovery Endpoint Missing" error when using MCPBuckle with MCPInvoke 1.4.0+

### Fixed
- **Tool Import Issues** - Resolved schema introspection problems that prevented MCPInvoke from importing tool schemas
- **Schema Information Access** - Fixed missing parametersSchema and returnSchema information in MCP tool discovery

## [1.5.1] - 2025-07-17

### Fixed
- Fixed AmbiguousMatchException in XmlDocumentationService when retrieving overloaded methods
- Fixed default value handling in McpTool and McpSchema configurations
- Fixed enum schema generation to properly handle JsonStringEnumConverter
- Fixed dictionary type detection to prevent misidentification as arrays
- Added circular reference detection in TypeSchemaGenerator to prevent stack overflow
- Replaced WebApplicationFactory with TestServer in integration tests to avoid entry point conflicts

### Added
- Comprehensive unit test suite covering all core functionality
- Integration tests using TestServer for testing MCP endpoint behavior
- Tests for circular reference handling in schema generation

### Changed
- Improved type checking order in TypeSchemaGenerator (dictionaries before arrays)
- Enhanced enum schema generation with dedicated GenerateEnumSchema method
- Updated default values: Description now defaults to empty string instead of null

## [1.0.0] - 2025-04-23

### Added
- Initial release of MCPBuckle
- Core MCP models (McpContext, McpTool, McpSchema)
- Controller discovery service to extract API metadata
- XML documentation parsing service
- Middleware to serve static MCP JSON at `/.well-known/mcp-context`
- Extension methods for easy integration
- Configuration options for customization
- Enhanced schema generation for complex types
- Support for required fields, enums, arrays, and nested objects
- Multi-targeting for .NET 6.0, 7.0, 8.0, and 9.0