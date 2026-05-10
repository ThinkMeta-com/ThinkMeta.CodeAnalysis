using VerifyCS = ThinkMeta.CodeAnalysis.Test.CSharpCodeFixVerifier<
    ThinkMeta.CodeAnalysis.NetAnalyzers.CloneMethodAnalyzer,
    ThinkMeta.CodeAnalysis.NetAnalyzers.CloneMethodCodeFixProvider>;

namespace ThinkMeta.CodeAnalysis.NetAnalyzers.Test;

[TestClass]
public class CloneMethodAnalyzerUnitTests
{
    [TestMethod]
    public async Task Test_TM0002_ObjectInitializer_MissingProperty_Async()
    {
        var test = """
            class C
            {
                public int X { get; set; }
                public int Y { get; set; }

                public C Clone()
                {
                    return new C { X = this.X };
                }
            }
            """;

        var fixtest = """
            class C
            {
                public int X { get; set; }
                public int Y { get; set; }

                public C Clone()
                {
                    return new C { X = this.X, Y = this.Y };
                }
            }
            """;

        var expected = VerifyCS.Diagnostic("TM0002").WithSpan(6, 14, 6, 19).WithArguments("'Y'");
        await VerifyCS.VerifyCodeFixAsync(test, expected, fixtest);
    }

    [TestMethod]
    public async Task Test_TM0002_ObjectInitializer_MultipleProperties_Async()
    {
        var test = """
            class C
            {
                public int X { get; set; }
                public int Y { get; set; }

                public C Clone()
                {
                    return new C { };
                }
            }
            """;

        var fixtest = """
            class C
            {
                public int X { get; set; }
                public int Y { get; set; }

                public C Clone()
                {
                    return new C { X = this.X, Y = this.Y };
                }
            }
            """;

        var expected = VerifyCS.Diagnostic("TM0002").WithSpan(6, 14, 6, 19).WithArguments("'X', 'Y'");
        await VerifyCS.VerifyCodeFixAsync(test, expected, fixtest);
    }

    [TestMethod]
    public async Task Test_TM0002_StatementBased_MultipleProperties_Async()
    {
        var test = """
            class C
            {
                public int X { get; set; }
                public int Y { get; set; }

                public C Clone()
                {
                    var clone = new C();
                    return clone;
                }
            }
            """;

        var fixtest = """
            class C
            {
                public int X { get; set; }
                public int Y { get; set; }

                public C Clone()
                {
                    var clone = new C();
                    clone.X = this.X;
                    clone.Y = this.Y;
                    return clone;
                }
            }
            """;

        var expected = VerifyCS.Diagnostic("TM0002").WithSpan(6, 14, 6, 19).WithArguments("'X', 'Y'");
        await VerifyCS.VerifyCodeFixAsync(test, expected, fixtest);
    }

    [TestMethod]
    public async Task Test_TM0002_StatementBased_MissingProperty_Async()
    {
        var test = """
            class C
            {
                public int X { get; set; }
                public int Y { get; set; }

                public C Clone()
                {
                    var clone = new C();
                    clone.X = this.X;
                    return clone;
                }
            }
            """;

        var fixtest = """
            class C
            {
                public int X { get; set; }
                public int Y { get; set; }

                public C Clone()
                {
                    var clone = new C();
                    clone.X = this.X;
                    clone.Y = this.Y;
                    return clone;
                }
            }
            """;

        var expected = VerifyCS.Diagnostic("TM0002").WithSpan(6, 14, 6, 19).WithArguments("'Y'");
        await VerifyCS.VerifyCodeFixAsync(test, expected, fixtest);
    }

    [TestMethod]
    public async Task Test_TM0003_ObjectInitializer_ShallowCopy_Async()
    {
        var test = """
            class D { }

            class C
            {
                public D Item { get; set; }

                public C Clone()
                {
                    return new C { Item = this.Item };
                }
            }
            """;

        var expected = VerifyCS.Diagnostic("TM0003").WithSpan(9, 31, 9, 40).WithArguments("Item");
        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    [TestMethod]
    public async Task Test_TM0003_StatementBased_ShallowCopy_Async()
    {
        var test = """
            class D { }

            class C
            {
                public D Item { get; set; }

                public C Clone()
                {
                    var clone = new C();
                    clone.Item = this.Item;
                    return clone;
                }
            }
            """;

        var expected = VerifyCS.Diagnostic("TM0003").WithSpan(10, 22, 10, 31).WithArguments("Item");
        await VerifyCS.VerifyAnalyzerAsync(test, expected);
    }

    [TestMethod]
    public async Task Test_NoDiagnostic_AllPropertiesAssigned_Async()
    {
        var test = """
            class C
            {
                public int X { get; set; }
                public int Y { get; set; }

                public C Clone()
                {
                    return new C { X = this.X, Y = this.Y };
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [TestMethod]
    public async Task Test_NoDiagnostic_MemberwiseClone_Async()
    {
        var test = """
            class C
            {
                public int X { get; set; }
                public int Y { get; set; }

                public C Clone() => (C)MemberwiseClone();
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [TestMethod]
    public async Task Test_NoDiagnostic_IgnoredProperty_Async()
    {
        var test = """
            class C
            {
                public int X { get; set; }
                public int IgnoredProp { get; set; }

                public C Clone()
                {
                    return new C { X = this.X };
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [TestMethod]
    public async Task Test_NoDiagnostic_GetOnlyProperty_Async()
    {
        var test = """
            class C
            {
                public int X { get; set; }
                public int Y { get; }

                public C Clone()
                {
                    return new C { X = this.X };
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [TestMethod]
    public async Task Test_NoDiagnostic_StringType_Async()
    {
        var test = """
            class C
            {
                public string Name { get; set; }

                public C Clone()
                {
                    return new C { Name = this.Name };
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(test);
    }

    [TestMethod]
    public async Task Test_TM0002_ICloneableReturnType_Async()
    {
        var test = """
            class C : System.ICloneable
            {
                public int X { get; set; }
                public int Y { get; set; }

                public object Clone()
                {
                    return new C { X = this.X };
                }
            }
            """;

        var fixtest = """
            class C : System.ICloneable
            {
                public int X { get; set; }
                public int Y { get; set; }

                public object Clone()
                {
                    return new C { X = this.X, Y = this.Y };
                }
            }
            """;

        var expected = VerifyCS.Diagnostic("TM0002").WithSpan(6, 19, 6, 24).WithArguments("'Y'");
        await VerifyCS.VerifyCodeFixAsync(test, expected, fixtest);
    }
}
