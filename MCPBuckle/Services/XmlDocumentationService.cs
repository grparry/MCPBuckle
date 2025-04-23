using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;

namespace MCPBuckle.Services
{
    /// <summary>
    /// Service for extracting documentation from XML documentation files.
    /// </summary>
    public class XmlDocumentationService
    {
        private readonly Dictionary<string, XmlDocument> _xmlDocCache = new Dictionary<string, XmlDocument>();

        /// <summary>
        /// Initializes a new instance of the <see cref="XmlDocumentationService"/> class.
        /// </summary>
        public XmlDocumentationService()
        {
            // Load XML documentation for the entry assembly
            LoadXmlDocumentation(Assembly.GetEntryAssembly());
        }

        /// <summary>
        /// Loads XML documentation for the specified assembly.
        /// </summary>
        /// <param name="assembly">The assembly to load documentation for.</param>
        public void LoadXmlDocumentation(Assembly? assembly)
        {
            if (assembly == null)
                return;

            string? assemblyName = assembly.GetName().Name;
            if (assemblyName == null)
                return;
            if (_xmlDocCache.ContainsKey(assemblyName))
                return;

            string? assemblyLocation = Path.GetDirectoryName(assembly.Location);
            if (assemblyLocation == null)
                return;
                
            string xmlFilePath = Path.Combine(
                assemblyLocation,
                $"{assemblyName}.xml");

            if (File.Exists(xmlFilePath))
            {
                try
                {
                    var xmlDoc = new XmlDocument();
                    xmlDoc.Load(xmlFilePath);
                    _xmlDocCache[assemblyName] = xmlDoc;
                }
                catch
                {
                    // Ignore errors loading XML documentation
                }
            }
        }

        /// <summary>
        /// Gets the XML documentation for a method.
        /// </summary>
        /// <param name="typeInfo">The type containing the method.</param>
        /// <param name="methodInfo">The method to get documentation for.</param>
        /// <returns>The method documentation, or null if not found.</returns>
        public string? GetMethodDocumentation(TypeInfo typeInfo, MethodInfo methodInfo)
        {
            string? assemblyName = typeInfo.Assembly.GetName().Name;
            if (assemblyName == null || !_xmlDocCache.TryGetValue(assemblyName, out var xmlDoc))
                return null;

            string memberName = $"M:{typeInfo.FullName}.{methodInfo.Name}";
            
            // Handle method parameters for overloaded methods
            if (methodInfo.GetParameters().Length > 0)
            {
                memberName += "(";
                memberName += string.Join(",", Array.ConvertAll(methodInfo.GetParameters(), p => p.ParameterType.FullName));
                memberName += ")";
            }

            var node = xmlDoc.SelectSingleNode($"//member[@name='{memberName}']/summary");
            if (node != null)
            {
                return CleanXmlText(node.InnerText);
            }

            return null;
        }

        /// <summary>
        /// Gets the XML documentation for a parameter.
        /// </summary>
        /// <param name="typeInfo">The type containing the method.</param>
        /// <param name="methodInfo">The method containing the parameter.</param>
        /// <param name="parameterName">The name of the parameter.</param>
        /// <returns>The parameter documentation, or null if not found.</returns>
        public string? GetParameterDocumentation(TypeInfo typeInfo, MethodInfo methodInfo, string parameterName)
        {
            string? assemblyName = typeInfo.Assembly.GetName().Name;
            if (assemblyName == null || !_xmlDocCache.TryGetValue(assemblyName, out var xmlDoc))
                return null;

            string memberName = $"M:{typeInfo.FullName}.{methodInfo.Name}";
            
            // Handle method parameters for overloaded methods
            if (methodInfo.GetParameters().Length > 0)
            {
                memberName += "(";
                memberName += string.Join(",", Array.ConvertAll(methodInfo.GetParameters(), p => p.ParameterType.FullName));
                memberName += ")";
            }

            var node = xmlDoc.SelectSingleNode($"//member[@name='{memberName}']/param[@name='{parameterName}']");
            if (node != null)
            {
                return CleanXmlText(node.InnerText);
            }

            return null;
        }

        /// <summary>
        /// Cleans XML text by removing leading/trailing whitespace and normalizing line breaks.
        /// </summary>
        /// <param name="text">The XML text to clean.</param>
        /// <returns>The cleaned text.</returns>
        private string CleanXmlText(string? text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            // Normalize line breaks
            text = text.Replace("\r\n", "\n").Replace("\r", "\n");

            // Split into lines
            var lines = text.Split('\n');

            // Trim each line and rejoin
            for (int i = 0; i < lines.Length; i++)
            {
                lines[i] = lines[i].Trim();
            }

            return string.Join(" ", lines).Trim();
        }
    }
}
