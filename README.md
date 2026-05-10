# ThinkMeta.CodeAnalysis
Static code analyzer for C#

[![NuGet Package](https://img.shields.io/nuget/v/ThinkMeta.CodeAnalysis.CSharp)](https://www.nuget.org/packages/ThinkMeta.CodeAnalysis.CSharp) ThinkMeta.CodeAnalysis.CSharp<br>

## Overview
ThinkMeta.CodeAnalysis is a static code analyzer for C# projects, built on Roslyn. It helps developers identify code issues, enforce coding standards, and improve code quality automatically.

## Features
- Detects common code issues and anti-patterns
- Supports custom analyzers and code fixes
- Integrates with .NET build and IDE tooling
- Targets .NET Standard 2.0 for broad compatibility

## Diagnostics

### TM0001: Use pattern matching for null checks

**Description:**  
Warns when using `== null` or `!= null` for null checks.  
**Reason:**  
Pattern matching (`is null`, `is not null`) is preferred for clarity and future-proofing code.  
**How to fix:**  
Replace `== null` with `is null`, and `!= null` with `is not null`.

**Exceptions:**  
Null checks using `== null` or `!= null` inside expression trees (e.g., lambdas assigned to `Expression<Func<...>>`) are not reported by this diagnostic, as pattern matching is not supported in expression trees.

**Examples:**

```csharp
// Standard null check
if (obj == null) { }
// =>
if (obj is null) { }

// Query syntax
var q = from o in arr where o == null select o;
// =>
var q = from o in arr where o is null select o;

// Razor-generated (in .g.cs from .razor)
@if (Model == null) { <text>Empty</text> }
// =>
@if (Model is null) { <text>Empty</text> }

// No warning in expression trees
Expression<Func<object, bool>> expr = o => o == null; // No diagnostic
```

### TM0002: Clone method is missing a property assignment

**Description:**  
Warns when a `Clone()` method (returning the containing type or `object`) does not assign all settable, non-excluded properties.  
**Reason:**  
Forgetting to assign a property in a `Clone()` method leads to silent data loss that is hard to detect at runtime.  
**How to fix:**  
The accompanying code fix adds all missing property assignments in a single edit, for both object-initializer and statement-based clone patterns.

**Exclusions:**  
Properties are skipped when their name or attribute name contains "clone", "ignore", or "exclude" (case-insensitive), when they have no setter, or when the method uses `MemberwiseClone()`.

**Examples:**

```csharp
// Missing property — TM0002 fires on "Clone"
class C
{
    public int X { get; set; }
    public int Y { get; set; }

    public C Clone() => new C { X = this.X }; // TM0002: does not assign: 'Y'
}

// After applying the code fix:
public C Clone() => new C { X = this.X, Y = this.Y };
```

### TM0003: Clone method performs a shallow copy of a reference-type property

**Description:**  
Warns when a `Clone()` method copies a reference-type property directly from `this`, producing a shallow copy.  
**Reason:**  
Shallow copying a reference type shares the same object between the original and the clone, which can lead to unintended mutations.  
**How to fix:**  
Replace `this.Prop` with a proper deep copy (e.g., `this.Prop.Clone()`, `new T(this.Prop)`, etc.).

**Exceptions:**  
`string` is exempt because it is immutable. Properties excluded from TM0002 (e.g., those whose name contains "ignore") are also excluded here.

**Examples:**

```csharp
class Inner { }

class C
{
    public Inner Item { get; set; }

    // TM0003 fires on "this.Item" — shallow copy of reference type
    public C Clone() => new C { Item = this.Item };
}
```
