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
        var test = """
            class A
            {
                void M(object o)
                {
                    if (o == null) { }
                }
            }
            """;

        var fixtest = """
            class A
            {
                void M(object o)
                {
                    if (o is null) { }
                }
            }
            """;

        var expected = VerifyCS.Diagnostic("TM0001").WithSpan(5, 13, 5, 22).WithArguments("is null", "== null");
        await VerifyCS.VerifyCodeFixAsync(test, expected, fixtest);
    }

    [TestMethod]
    public async Task Test_IsNotNull_Async()
    {
        var test = """
            class A
            {
                void M(object o)
                {
                    if (o != null) { }
                }
            }
            """;

        var fixtest = """
            class A
            {
                void M(object o)
                {
                    if (o is not null) { }
                }
            }
            """;

        var expected = VerifyCS.Diagnostic("TM0001").WithSpan(5, 13, 5, 22).WithArguments("is not null", "!= null");
        await VerifyCS.VerifyCodeFixAsync(test, expected, fixtest);
    }

    [TestMethod]
    public async Task TestNoDiagnosticInsideExpressionTree_IsNullAsync()
    {
        var test = """
            using System;
            using System.Linq.Expressions;
            
            class A
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
            
            class A
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

            class A
            {
                bool Inner(Expression<Func<object, bool>> o)
                {
                    return true;
                }

                void M()
                {
                    Expression<Func<object, bool>> outer = o => Inner(o => o == null);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [TestMethod]
    public async Task Test_QuerySyntax_IsNull_Async()
    {
        var test = """
            using System.Linq;

            class A
            {
                void M(object[] arr)
                {
                    var q = from o in arr where o == null select o;
                }
            }
            """;

        var fixtest = """
            using System.Linq;
            
            class A
            {
                void M(object[] arr)
                {
                    var q = from o in arr where o is null select o;
                }
            }
            """;

        var expected = VerifyCS.Diagnostic("TM0001").WithSpan(7, 37, 7, 46).WithArguments("is null", "== null");
        await VerifyCS.VerifyCodeFixAsync(test, expected, fixtest);
    }

    [TestMethod]
    public async Task Test_QuerySyntax_IsNotNull_Async()
    {
        var test = """
            using System.Linq;
            
            class A
            {
                void M(object[] arr)
                {
                    var q = from o in arr where o != null select o;
                }
            }
            """;

        var fixtest = """
            using System.Linq;
            
            class A
            {
                void M(object[] arr)
                {
                    var q = from o in arr where o is not null select o;
                }
            }
            """;

        var expected = VerifyCS.Diagnostic("TM0001").WithSpan(7, 37, 7, 46).WithArguments("is not null", "!= null");
        await VerifyCS.VerifyCodeFixAsync(test, expected, fixtest);
    }
}
