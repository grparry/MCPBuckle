using System;
using Microsoft.Extensions.DependencyInjection;
using MCPBuckle.Configuration;
using MCPBuckle.Services;

namespace MCPBuckle.Extensions
{
    /// <summary>
    /// Extension methods for <see cref="IServiceCollection"/> to add MCPBuckle services.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds MCPBuckle services to the service collection with default options.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddMcpBuckle(this IServiceCollection services)
        {
            return services.AddMcpBuckle(options => { });
        }

        /// <summary>
        /// Adds MCPBuckle services to the service collection with custom options.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="setupAction">The action to configure the MCPBuckle options.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddMcpBuckle(
            this IServiceCollection services,
            Action<McpBuckleOptions> setupAction)
        {
            // Configure options
            services.Configure<McpBuckleOptions>(setupAction);

            // Register the controller discovery service as a singleton
            services.AddSingleton<ControllerDiscoveryService>();
            services.AddSingleton<IControllerDiscoveryService>(sp => sp.GetRequiredService<ControllerDiscoveryService>());
            
            // Register the MCP context generator as a singleton
            services.AddSingleton<McpContextGenerator>();
            services.AddSingleton<IContextGenerator>(sp => sp.GetRequiredService<McpContextGenerator>());
            
            // Register the XML documentation service as a singleton
            services.AddSingleton<XmlDocumentationService>();

            // Register the type schema generator as a singleton
            services.AddSingleton<TypeSchemaGenerator>();

            return services;
        }
    }
}
