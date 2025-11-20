using VerifyCS = ThinkMeta.CodeAnalysis.Test.CSharpCodeFixVerifier<
    ThinkMeta.CodeAnalysis.NetAnalyzers.NullEqualityAnalyzer,
    ThinkMeta.CodeAnalysis.NetAnalyzers.NullEqualityCodeFixProvider>;

[assembly: Parallelize]

namespace ThinkMeta.CodeAnalysis.NetAnalyzers.Test;

[TestClass]
public class NullEqualityAnalyzerUnitTests
{
    [TestMethod]
    public async Task TestIsNullAsync()
    {
        var test = "namespace A { class B { void M(object o) { if (o == null) { } } } }";
        var fixtest = "namespace A { class B { void M(object o) { if (o is null) { } } } }";

        var expected = VerifyCS.Diagnostic("TM0001").WithSpan(1, 48, 1, 57).WithArguments("is null", "== null");
        await VerifyCS.VerifyCodeFixAsync(test, expected, fixtest);
    }

    [TestMethod]
    public async Task TestIsNotNullAsync()
    {
        var test = "namespace A { class B { void M(object o) { if (o != null) { } } } }";
        var fixtest = "namespace A { class B { void M(object o) { if (o is not null) { } } } }";

        var expected = VerifyCS.Diagnostic("TM0001").WithSpan(1, 48, 1, 57).WithArguments("is not null", "!= null");
        await VerifyCS.VerifyCodeFixAsync(test, expected, fixtest);
    }
}
