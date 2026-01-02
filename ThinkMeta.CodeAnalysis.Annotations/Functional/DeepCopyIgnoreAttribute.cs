using System.Diagnostics;

namespace ThinkMeta.CodeAnalysis.Annotations.Functional;

/// <summary>
/// Indicates that a property or field should be ignored during deep copy operations.
/// </summary>
[Conditional("CODE_ANALYSIS")]
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class DeepCopyIgnoreAttribute : Attribute { }