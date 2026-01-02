using System.Diagnostics;

namespace ThinkMeta.CodeAnalysis.Annotations.Functional;

/// <summary>
/// Indicates that a method performs a deep copy.
/// </summary>
[Conditional("CODE_ANALYSIS")]
[AttributeUsage(AttributeTargets.Method)]
public sealed class DeepCopyAttribute : Attribute { }
