using VerifyCS = ThinkMeta.CodeAnalysis.Test.CSharpCodeFixVerifier<
    ThinkMeta.CodeAnalysis.NetAnalyzers.NullEqualityAnalyzer,
    ThinkMeta.CodeAnalysis.NetAnalyzers.NullEqualityCodeFixProvider>;

[assembly: Parallelize]

namespace ThinkMeta.CodeAnalysis.NetAnalyzers.Test;

[TestClass]
public class NullEqualityAnalyzerUnitTests
{
    [TestMethod]
    public async Task Test_IsNull_Async()
    {
        var test = "namespace A { class B { void M(object o) { if (o == null) { } } } }";
        var fixtest = "namespace A { class B { void M(object o) { if (o is null) { } } } }";

        var expected = VerifyCS.Diagnostic("TM0001").WithSpan(1, 48, 1, 57).WithArguments("is null", "== null");
        await VerifyCS.VerifyCodeFixAsync(test, expected, fixtest);
    }

    [TestMethod]
    public async Task Test_IsNotNull_Async()
    {
        var test = "namespace A { class B { void M(object o) { if (o != null) { } } } }";
        var fixtest = "namespace A { class B { void M(object o) { if (o is not null) { } } } }";

        var expected = VerifyCS.Diagnostic("TM0001").WithSpan(1, 48, 1, 57).WithArguments("is not null", "!= null");
        await VerifyCS.VerifyCodeFixAsync(test, expected, fixtest);
    }

    [TestMethod]
    public async Task TestNoDiagnosticInsideExpressionTree_IsNullAsync()
    {
        var test = """
            using System;
            using System.Linq.Expressions;
            
            namespace A;
            
            class B
            {
                void M()
                {
                    Expression<Func<object, bool>> expr = o => o == null;
                } 
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [TestMethod]
    public async Task TestNoDiagnosticInsideExpressionTree_IsNotNullAsync()
    {
        var test = """
            using System;
            using System.Linq.Expressions;
            
            namespace A;
            
            class B
            {
                void M()
                {
                    Expression<Func<object, bool>> expr = o => o != null;
                } 
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [TestMethod]
    public async Task TestNoDiagnosticInsideNestedExpressionTree_IsNullAsync()
    {
        var test = """
            using System;
            using System.Linq.Expressions;

            namespace A;

            class B
            {
                void M()
                {
                    Expression<Func<object, bool>> outer = o =>
                        {
                            Expression<Func<object, bool>> inner = x => x == null;
                            return o == null;
                        };
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }
}
