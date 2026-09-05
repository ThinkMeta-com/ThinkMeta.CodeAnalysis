# Release Notes

## 1.0.17
- Fixed false positives in NullEqualityAnalyzer (TM0001) for LINQ query syntax translated into expression trees (e.g. queries over `IQueryable`/EF Core sources), where the suggested pattern-matching replacement is not valid C#.

## 1.0.16
- Split `ThinkMeta.CodeAnalysis.Annotations` into a separate NuGet package (`ThinkMeta.CodeAnalysis.Annotations`), declared as a dependency of `ThinkMeta.CodeAnalysis.CSharp`.

## 1.0.15
- Fixed runtime availability of `CloneIgnoreAttribute` by shipping it as a compiled assembly in `lib/netstandard2.0`.

## 1.0.13
- Added `CloneIgnoreAttribute` to explicitly exclude properties from Clone analysis at method or assembly level.
- Private properties are no longer reported by TM0002 or TM0003.

## 1.0.12
- Added Clone method completeness analyzer (TM0002) to detect missing property assignments in `Clone()` methods, with a code fix to add all missing assignments in a single edit.
- Added Clone method shallow copy analyzer (TM0003) to warn when reference-type properties are shallow-copied inside `Clone()` methods.

## 1.0.6
- Added DeepCopy usage analyzer (TM0002) to enforce correct DeepCopy attribute usage and sealed types.
- Added DeepCopy method completeness analyzer (TM0003) to warn on incomplete DeepCopy implementations.

## 1.0.5
- Improved Razor and query syntax support in NullEqualityAnalyzer.

## 1.0.4
- Fixed false positives in nested expression trees.

## 1.0.1
- Renamed project files due to naming conflicts with nuget packages.

## 1.0.0
- Added Null Equality Analyzer to improve code quality by detecting improper null comparisons.
