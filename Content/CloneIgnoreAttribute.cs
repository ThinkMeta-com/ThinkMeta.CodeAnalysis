#nullable enable

using System;

namespace ThinkMeta.CodeAnalysis
{
    /// <summary>
    /// Excludes one or more properties from Clone method analysis (TM0002 and TM0003).
    /// Apply to a <c>Clone()</c> method to exclude specific properties for that method only,
    /// or at assembly level to exclude properties globally across all Clone methods.
    /// </summary>
    /// <example>
    /// Method-level — exclude a property from one Clone method:
    /// <code>
    /// [CloneIgnore(nameof(AdditionalProperties))]
    /// public MyClass Clone() { ... }
    /// </code>
    /// Assembly-level — exclude a property from all Clone methods in the assembly:
    /// <code>
    /// [assembly: CloneIgnore("AdditionalProperties")]
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Assembly, AllowMultiple = true)]
    internal sealed class CloneIgnoreAttribute : Attribute
    {
        /// <summary>Initializes the attribute with the names of properties to exclude from Clone analysis.</summary>
        public CloneIgnoreAttribute(params string[] properties)
        {
            Properties = properties;
        }

        /// <summary>Gets the names of the properties to exclude.</summary>
        public string[] Properties { get; }
    }
}
