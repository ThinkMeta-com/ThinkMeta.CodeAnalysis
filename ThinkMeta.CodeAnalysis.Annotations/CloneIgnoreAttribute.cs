namespace ThinkMeta.CodeAnalysis.Annotations;

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
/// [assembly: ThinkMeta.CodeAnalysis.Annotations.CloneIgnore("AdditionalProperties")]
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class CloneIgnoreAttribute(params string[] properties) : Attribute
{
    /// <summary>Gets the names of the properties to exclude.</summary>
    public string[] Properties { get; } = properties;
}
