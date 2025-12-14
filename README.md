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
