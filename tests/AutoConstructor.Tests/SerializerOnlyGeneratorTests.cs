using Xunit;
using VerifySourceGenerator = AutoConstructor.Tests.Verifiers.CSharpSourceGeneratorVerifier<AutoConstructor.Generator.AutoConstructorGenerator>;

namespace AutoConstructor.Tests;

public class SerializerOnlyGeneratorTests
{

    private static string _generatedFileName => "Test.Test.ser.g.cs";
    [Fact]
    public async Task Run_WithAttributeAndPartial_ShouldGenerateClass()
    {
        const string code = @"
namespace Test
{
    [SerializerConstructor]
    internal partial class Test
    {
        public int T {get; set; }
        public string B {get; private set; }
        public string C {get;}

        public Test(int t, string b, string c){
            T = t;
            B = b;
            C = c;
        }
    }
}";
        const string generated = @"namespace Test
{
    partial class Test
    {
        public Test()
        {
        }
    }
}
";
        await VerifySourceGenerator.RunAsync(code, generated, generatedName: _generatedFileName, expectSerializerConstructor: true);
    }

    [Fact]
    public async Task Run_WithBasicInheritance_ShouldGenerateClass()
    {
        const string code = @"
namespace Test
{
    internal class BaseClass
    {
        private readonly int _t;
        public BaseClass(int t)
        {
            this._t = t;
        }

        [System.Obsolete]
        public BaseClass(){}
    }

    [SerializerConstructor]
    internal partial class Test : BaseClass
    {
    }
}";
        const string generated = @"namespace Test
{
    partial class Test
    {
        public Test()
        {
        }
    }
}
";
        await VerifySourceGenerator.RunAsync(code, generated, generatedName: _generatedFileName,  expectSerializerConstructor: true);
    }

    [Fact]
    public async Task Run_WithInheritanceAlsoGenerated_ShouldGenerateClass()
    {
        const string code = @"
namespace Test
{
    [SerializerConstructor]
    internal partial class BaseClass
    {
        public int T {get; set; }

        public BaseClass(int t) {
            T = t;
        }
    }
    [SerializerConstructor]
    internal partial class Test : BaseClass
    {
    }
}";
        const string generatedTest = @"namespace Test
{
    partial class Test
    {
        public Test()
        {
        }
    }
}
";

        const string generatedBase = @"namespace Test
{
    partial class BaseClass
    {
        public BaseClass()
        {
        }
    }
}
";
        await VerifySourceGenerator.RunAsync(code, new[] { (generatedBase, "Test.BaseClass.ser.g.cs"), (generatedTest, _generatedFileName) }, expectSerializerConstructor: true);
    }
}
