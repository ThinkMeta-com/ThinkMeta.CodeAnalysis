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

````````
### TM0002: DeepCopy attribute usage violation

**Description:**  
Reports errors when a method marked with `[DeepCopy]` does not follow required usage rules.

**Reason:**  
Methods with `[DeepCopy]` must have exactly one parameter, and that parameter's type (and all nested types/collections, unless marked with `[DeepCopyIgnore]`) must be `sealed`.

**How to fix:**  
- Ensure the method has only one parameter.
- The parameter type and all nested types/collections must be `sealed` (unless `[DeepCopyIgnore]` is used).

**Examples:**

```csharp
// Violations:
class Bar {
    [DeepCopy]
    void Copy(int a, int b) { } // TM0002: Method must have exactly one parameter.
}

class Foo { }
class Bar {
    [DeepCopy]
    void Copy(Foo foo) { } // TM0002: Parameter type 'Foo' must be sealed.
}

class Inner { }
sealed class Outer { public Inner I; }
class Bar {
    [DeepCopy]
    void Copy(Outer outer) { } // TM0002: All nested types and collections must be sealed.
}

// Correct:
sealed class Inner { }
sealed class Outer { public Inner I; }
class Bar {
    [DeepCopy]
    void Copy(Outer outer) { } // OK
}
```

### TM0003: DeepCopy method incomplete

**Description:**  
Warns when a method marked with `[DeepCopy]` is incomplete, such as containing only a `throw new NotImplementedException()` or is empty.

**Reason:**  
A `[DeepCopy]` method should provide a full implementation for deep copying the parameter.

**How to fix:**  
Implement the method body to perform a deep copy of the parameter.

**Examples:**

```csharp
class Bar {
    [DeepCopy]
    void Copy(Foo foo) {
        throw new NotImplementedException(); // TM0003: DeepCopy method incomplete.
    }
}

// Correct:
class Bar {
    [DeepCopy]
    void Copy(Foo foo) {
        // ... actual deep copy logic ...
    }
}
