using System;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MCPBuckle.Configuration;
using MCPBuckle.Services;

namespace MCPBuckle.Extensions
{
    /// <summary>
    /// Extension methods for configuring controller-based MCP tool discovery.
    /// </summary>
    public static class ControllerDiscoveryExtensions
    {
        /// <summary>
        /// Adds controller-based MCP tool discovery with the specified assemblies for scanning.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="assemblies">Assemblies to scan for controllers. If not provided, the calling assembly is used.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddMcpControllerDiscovery(
            this IServiceCollection services,
            params Assembly[] assemblies)
        {
            return services.AddMcpControllerDiscovery(options => { }, assemblies);
        }

        /// <summary>
        /// Adds controller-based MCP tool discovery with custom options and the specified assemblies for scanning.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="setupAction">The action to configure the MCPBuckle options.</param>
        /// <param name="assemblies">Assemblies to scan for controllers. If not provided, the calling assembly is used.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddMcpControllerDiscovery(
            this IServiceCollection services,
            Action<McpBuckleOptions> setupAction,
            params Assembly[] assemblies)
        {
            // First, add the base MCPBuckle services
            services.AddMcpBuckle(options => 
            {
                // Set default options
                options.IncludeControllerNameInToolName = true;

                // Apply custom options
                setupAction(options);
            });

            // Configure assembly scanning if assemblies were provided
            if (assemblies.Length > 0)
            {
                services.Configure<McpBuckleOptions>(options =>
                {
                    options.AssembliesToScan = assemblies.ToList();
                });
            }

            return services;
        }
    }
}
