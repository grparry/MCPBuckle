# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
