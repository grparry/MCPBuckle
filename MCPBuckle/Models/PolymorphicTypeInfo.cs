using System;
using System.Collections.Generic;

namespace MCPBuckle.Models
{
    /// <summary>
    /// Holds information about a detected polymorphic type hierarchy.
    /// Used during schema generation to properly create oneOf schemas with discriminators.
    /// </summary>
    public class PolymorphicTypeInfo
    {
        /// <summary>
        /// Gets or sets the base type of the polymorphic hierarchy.
        /// </summary>
        public Type BaseType { get; set; } = null!;

        /// <summary>
        /// Gets or sets the name of the discriminator property.
        /// </summary>
        public string DiscriminatorPropertyName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the mapping of discriminator values to derived types.
        /// Key is the discriminator value (e.g., "text", "stack"),
        /// Value is the derived Type.
        /// </summary>
        public Dictionary<string, Type> DerivedTypes { get; set; } = new Dictionary<string, Type>();

        /// <summary>
        /// Gets or sets whether the discriminator value should match the type name
        /// (used when JsonDerivedType doesn't specify an explicit discriminator).
        /// </summary>
        public bool UseTypeNameAsDiscriminator { get; set; }
    }

    /// <summary>
    /// Represents a single derived type in a polymorphic hierarchy.
    /// </summary>
    public class DerivedTypeInfo
    {
        /// <summary>
        /// Gets or sets the derived type.
        /// </summary>
        public Type Type { get; set; } = null!;

        /// <summary>
        /// Gets or sets the discriminator value for this derived type.
        /// </summary>
        public string DiscriminatorValue { get; set; } = string.Empty;
    }
}
