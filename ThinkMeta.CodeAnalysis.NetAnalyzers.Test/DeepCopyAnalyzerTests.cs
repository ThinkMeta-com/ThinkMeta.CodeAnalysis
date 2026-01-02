using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Diagnostics;
using ThinkMeta.CodeAnalysis.NetAnalyzers;
using ThinkMeta.CodeAnalysis.Test;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ThinkMeta.CodeAnalysis.Annotations.Functional;

namespace ThinkMeta.CodeAnalysis.NetAnalyzers.Test;

[TestClass]
public class DeepCopyAnalyzerTests
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
    public async Task ReportsDiagnostic_WhenPublicMembersNotCopiedAsync()
    {
        var source = FileHeader + """
            sealed class Foo {
                public int A;
                public int B;
            }

            class Bar {
                [DeepCopy]
                void Copy(Foo foo) {
                    var x = foo.A;
                    // foo.B is not copied
                }
            }
            """;

        var expected = CSharpAnalyzerVerifier<DeepCopyAnalyzer>.Diagnostic("TM0003")
            .WithSpan(17, 10, 17, 14)
            .WithArguments("Copy", "B");
        await CSharpAnalyzerVerifier<DeepCopyAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [TestMethod]
    public async Task NoDiagnostic_WhenAllMembersCopiedAsync()
    {
        var source = FileHeader + """
            sealed class Foo {
                public int A;
                public int B;
            }

            class Bar {
                [DeepCopy]
                void Copy(Foo foo) {
                    var x = foo.A;
                    var y = foo.B;
                }
            }
            """;

        await CSharpAnalyzerVerifier<DeepCopyAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [TestMethod]
    public async Task NoDiagnostic_WhenNotMarkedWithDeepCopyAsync()
    {
        var source = FileHeader + """
            sealed class Foo {
                public int A;
                public int B;
            }

            class Bar {
                void Copy(Foo foo) {
                    var x = foo.A;
                }
            }
            """;

        await CSharpAnalyzerVerifier<DeepCopyAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [TestMethod]
    public async Task ReportsDiagnostic_WhenNestedTypeMembersNotCopiedAsync()
    {
        var source = FileHeader + """
            sealed class Inner {
                public int X;
                public int Y;
            }

            sealed class Outer {
                public Inner I;
            }

            class Bar {
                [DeepCopy]
                void Copy(Outer outer) {
                    var x = outer.I.X;
                    // outer.I.Y is not copied
                }
            }
            """;

        var expected = CSharpAnalyzerVerifier<DeepCopyAnalyzer>.Diagnostic("TM0003")
            .WithSpan(21, 10, 21, 14)
            .WithArguments("Copy", "I.Y");
        await CSharpAnalyzerVerifier<DeepCopyAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [TestMethod]
    public async Task NoDiagnostic_WhenAllNestedTypeMembersCopiedAsync()
    {
        var source = FileHeader + """
            sealed class Inner {
                public int X;
                public int Y;
            }

            sealed class Outer {
                public Inner I;
            }

            class Bar {
                [DeepCopy]
                void Copy(Outer outer) {
                    var x = outer.I.X;
                    var y = outer.I.Y;
                }
            }
            """;

        await CSharpAnalyzerVerifier<DeepCopyAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [TestMethod]
    public async Task ReportsDiagnostic_WhenCollectionNotCopiedAsync()
    {
        var source = FileHeader + """
            sealed class Foo {
                public List<int> Items;
            }

            class Bar {
                [DeepCopy]
                void Copy(Foo foo) {
                    // foo.Items is not copied
                }
            }
            """;

        var expected = CSharpAnalyzerVerifier<DeepCopyAnalyzer>.Diagnostic("TM0003")
            .WithSpan(16, 10, 16, 14)
            .WithArguments("Copy", "Items");
        await CSharpAnalyzerVerifier<DeepCopyAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [TestMethod]
    public async Task NoDiagnostic_WhenCollectionCopiedAsync()
    {
        var source = FileHeader + """
            sealed class Foo {
                public List<int> Items;
            }

            class Bar {
                [DeepCopy]
                void Copy(Foo foo) {
                    foreach (var item in foo.Items) { }
                }
            }
            """;

        await CSharpAnalyzerVerifier<DeepCopyAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [TestMethod]
    public async Task ReportsDiagnostic_WhenNestedTypeCollectionNotCopiedAsync()
    {
        var source = FileHeader + """
            sealed class Inner {
                public List<int> Items;
            }

            sealed class Outer {
                public Inner I;
            }

            class Bar {
                [DeepCopy]
                void Copy(Outer outer) {
                    // outer.I.Items is not copied
                }
            }
            """;

        var expected = CSharpAnalyzerVerifier<DeepCopyAnalyzer>.Diagnostic("TM0003")
            .WithSpan(20, 10, 20, 14)
            .WithArguments("Copy", "I.Items");
        await CSharpAnalyzerVerifier<DeepCopyAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [TestMethod]
    public async Task NoDiagnostic_WhenNestedTypeCollectionCopiedAsync()
    {
        var source = FileHeader + """
            sealed class Inner {
                public List<int> Items;
            }

            sealed class Outer {
                public Inner I;
            }

            class Bar {
                [DeepCopy]
                void Copy(Outer outer) {
                    foreach (var item in outer.I.Items) { }
                }
            }
            """;

        await CSharpAnalyzerVerifier<DeepCopyAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [TestMethod]
    public async Task ReportsDiagnostic_WhenMultipleMembersNotCopiedAsync()
    {
        var source = FileHeader + """
            sealed class Foo {
                public int A;
                public int B;
                public int C;
            }

            class Bar {
                [DeepCopy]
                void Copy(Foo foo) {
                    var x = foo.A;
                    // foo.B and foo.C are not copied
                }
            }
            """;

        var expected = CSharpAnalyzerVerifier<DeepCopyAnalyzer>.Diagnostic("TM0003")
            .WithSpan(18, 10, 18, 14)
            .WithArguments("Copy", "B, C");
        await CSharpAnalyzerVerifier<DeepCopyAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [TestMethod]
    public async Task NoDiagnostic_WhenMemberWithDeepCopyIgnoreIsNotCopiedAsync()
    {
        var source = FileHeader + """
            sealed class Foo {
                public int A;
                [DeepCopyIgnore]
                public int B;
            }

            class Bar {
                [DeepCopy]
                void Copy(Foo foo) {
                    var x = foo.A;
                    // foo.B is not copied, but ignored
                }
            }
            """;

        await CSharpAnalyzerVerifier<DeepCopyAnalyzer>.VerifyAnalyzerAsync(source);
    }

    [TestMethod]
    public async Task ReportsDiagnostic_WhenNonIgnoredMemberIsNotCopiedAsync()
    {
        var source = FileHeader + """
            sealed class Foo {
                public int A;
                [DeepCopyIgnore]
                public int B;
                public int C;
            }

            class Bar {
                [DeepCopy]
                void Copy(Foo foo) {
                    var x = foo.A;
                    // foo.C is not copied, foo.B is ignored
                }
            }
            """;

        var expected = CSharpAnalyzerVerifier<DeepCopyAnalyzer>.Diagnostic("TM0003")
            .WithSpan(19, 10, 19, 14)
            .WithArguments("Copy", "C");
        await CSharpAnalyzerVerifier<DeepCopyAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    [TestMethod]
    public async Task NoDiagnostic_WhenAllNonIgnoredMembersCopiedAsync()
    {
        var source = FileHeader + """
            sealed class Foo {
                public int A;
                [DeepCopyIgnore]
                public int B;
                public int C;
            }

            class Bar {
                [DeepCopy]
                void Copy(Foo foo) {
                    var x = foo.A;
                    var y = foo.C;
                    // foo.B is ignored
                }
            }
            """;

        await CSharpAnalyzerVerifier<DeepCopyAnalyzer>.VerifyAnalyzerAsync(source);
    }
}