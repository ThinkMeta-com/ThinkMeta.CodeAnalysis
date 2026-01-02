using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ThinkMeta.CodeAnalysis.NetAnalyzers;
using ThinkMeta.CodeAnalysis.Test;

namespace ThinkMeta.CodeAnalysis.NetAnalyzers.Test;

[TestClass]
public class DeepCopyUsageAnalyzerTests
{
    private const string FileHeader = """
        using System;
        using System.Collections.Generic;
        using ThinkMeta.CodeAnalysis.Annotations.Functional;

        namespace ThinkMeta.CodeAnalysis.Annotations.Functional {
            [AttributeUsage(AttributeTargets.Method)]
            public sealed class DeepCopyAttribute : Attribute { }
            [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
            public sealed class DeepCopyIgnoreAttribute : Attribute { }
        }
        """;

    [TestMethod]
    public async Task ReportsDiagnostic_WhenMethodHasMultipleParametersAsync()
    {
        var source = FileHeader + """
            class Bar {
                [DeepCopy]
                void Copy(int a, int b) { }
            }
            """;

        var expected = CSharpAnalyzerVerifier<DeepCopyUsageAnalyzer>.Diagnostic("TM0002")
            .WithSpan(12, 10, 12, 14)
            .WithArguments("Method must have exactly one parameter.");
        await CSharpAnalyzerVerifier<DeepCopyUsageAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [TestMethod]
    public async Task ReportsDiagnostic_WhenParameterTypeIsNotSealedAsync()
    {
        var source = FileHeader + """
            class Foo { }
            class Bar {
                [DeepCopy]
                void Copy(Foo foo) { }
            }
            """;

        var expected = CSharpAnalyzerVerifier<DeepCopyUsageAnalyzer>.Diagnostic("TM0002")
            .WithSpan(13, 19, 13, 22)
            .WithArguments("Parameter type 'Foo' must be sealed.");
        await CSharpAnalyzerVerifier<DeepCopyUsageAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [TestMethod]
    public async Task ReportsDiagnostic_WhenNestedTypeIsNotSealedAsync()
    {
        var source = FileHeader + """
            class Inner { }
            sealed class Outer { public Inner I; }
            class Bar {
                [DeepCopy]
                void Copy(Outer outer) { }
            }
            """;

        var expected = CSharpAnalyzerVerifier<DeepCopyUsageAnalyzer>.Diagnostic("TM0002")
            .WithSpan(14, 21, 14, 26)
            .WithArguments("All nested types and collections (not marked with DeepCopyIgnore) must be sealed.");
        await CSharpAnalyzerVerifier<DeepCopyUsageAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [TestMethod]
    public async Task NoDiagnostic_WhenParameterAndNestedTypesAreSealedAsync()
    {
        var source = FileHeader + """
            sealed class Inner { }
            sealed class Outer { public Inner I; }
            class Bar {
                [DeepCopy]
                void Copy(Outer outer) { }
            }
            """;

        await CSharpAnalyzerVerifier<DeepCopyUsageAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [TestMethod]
    public async Task NoDiagnostic_WhenNestedTypeIsIgnoredAsync()
    {
        var source = FileHeader + """
            class Inner { }
            sealed class Outer { [DeepCopyIgnore] public Inner I; }
            class Bar {
                [DeepCopy]
                void Copy(Outer outer) { }
            }
            """;

        await CSharpAnalyzerVerifier<DeepCopyUsageAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [TestMethod]
    public async Task ReportsDiagnostic_WhenCollectionElementTypeIsNotSealedAsync()
    {
        var source = FileHeader + """
            class Item { }
            sealed class Foo { public System.Collections.Generic.List<Item> Items; }
            class Bar {
                [DeepCopy]
                void Copy(Foo foo) { }
            }
            """;

        var expected = CSharpAnalyzerVerifier<DeepCopyUsageAnalyzer>.Diagnostic("TM0002")
            .WithSpan(14, 19, 14, 22)
            .WithArguments("All nested types and collections (not marked with DeepCopyIgnore) must be sealed.");
        await CSharpAnalyzerVerifier<DeepCopyUsageAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [TestMethod]
    public async Task NoDiagnostic_WhenCollectionElementTypeIsSealedAsync()
    {
        var source = FileHeader + """
            sealed class Item { }
            sealed class Foo { public List<Item> Items; }
            class Bar {
                [DeepCopy]
                void Copy(Foo foo) { }
            }
            """;

        await CSharpAnalyzerVerifier<DeepCopyUsageAnalyzer>.VerifyAnalyzerAsync(source);
    }
}
