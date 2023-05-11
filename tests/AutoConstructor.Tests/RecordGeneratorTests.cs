using System.Globalization;
using AutoConstructor.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifySourceGenerator = AutoConstructor.Tests.Verifiers.CSharpSourceGeneratorVerifier<AutoConstructor.Generator.CombinedAutoConstructorGenerator>;

namespace AutoConstructor.Tests;

public class RecordGeneratorTests
{
    [Fact]
    public async Task Run_WithAttributeAndPartial_ShouldGenerateRecord()
    {
        const string code = @"
namespace Test
{
    [AutoConstructor]
    internal partial record Test
    {
        private readonly int _t;
    }
}";
        const string generated = @"namespace Test
{
    partial record Test
    {
        public Test(int t)
        {
            this._t = t;
        }
    }
}
";
        await VerifySourceGenerator.RunAsync(code, generated);
    }

    [Fact]
    public async Task Run_WithAttributeShortSyntax_ShouldGenerateRecord()
    {
        const string code = @"
namespace Test
{
    [AutoConstructor]
    internal partial record Test : TestBase {
        public int Age {get;}
    }

    record TestBase (double Weight);

}";
        const string generated = @"namespace Test
{
    partial record Test
    {
        public Test(int age, double Weight) : base(Weight)
        {
            this.Age = age;
        }
    }
}
";
        await VerifySourceGenerator.RunAsync(code, generated);
    }

    [Theory]
    [InlineData(@"
namespace Test
{
    [AutoConstructor]
    internal partial record Test
    {
        [AutoConstructorInject(""guid.ToString()"", ""guid"", typeof(System.Guid))]
        private readonly string _guidString;
    }
}", @"namespace Test
{
    partial record Test
    {
        public Test(System.Guid guid)
        {
            this._guidString = guid.ToString() ?? throw new System.ArgumentNullException(nameof(guid));
        }
    }
}
")]
    [InlineData(@"
namespace Test
{
    [AutoConstructor]
    internal partial record Test
    {
        [AutoConstructorInject(injectedType: typeof(System.Guid), parameterName: ""guid"", initializer: ""guid.ToString()"")]
        private readonly string _guidString;
    }
}", @"namespace Test
{
    partial record Test
    {
        public Test(System.Guid guid)
        {
            this._guidString = guid.ToString() ?? throw new System.ArgumentNullException(nameof(guid));
        }
    }
}
")]
    [InlineData(@"
namespace Test
{
    [AutoConstructor]
    internal partial record Test
    {
        [AutoConstructorInject(null, ""guid"", typeof(string))]
        private readonly string _guidString;
    }
}", @"namespace Test
{
    partial record Test
    {
        public Test(string guid)
        {
            this._guidString = guid ?? throw new System.ArgumentNullException(nameof(guid));
        }
    }
}
")]
    [InlineData(@"
namespace Test
{
    [AutoConstructor]
    internal partial record Test
    {
        private readonly System.Guid _guid;
        [AutoConstructorInject(""guid.ToString()"", ""guid"", null)]
        private readonly string _guidString;
    }
}", @"namespace Test
{
    partial record Test
    {
        public Test(System.Guid guid)
        {
            this._guid = guid;
            this._guidString = guid.ToString() ?? throw new System.ArgumentNullException(nameof(guid));
        }
    }
}
")]
    [InlineData(@"
namespace Test
{
    [AutoConstructor]
    internal partial record Test
    {
        [AutoConstructorInject(""guid.ToString()"", ""guid"", null)]
        private readonly string _guidString;
    }
}", @"namespace Test
{
    partial record Test
    {
        public Test(string guid)
        {
            this._guidString = guid.ToString() ?? throw new System.ArgumentNullException(nameof(guid));
        }
    }
}
")]
    [InlineData(@"
namespace Test
{
    [AutoConstructor]
    internal partial record Test
    {
        [AutoConstructorInject(initializer: ""guid.ToString()"", injectedType: typeof(System.Guid))]
        private readonly string _guid;
    }
}", @"namespace Test
{
    partial record Test
    {
        public Test(System.Guid guid)
        {
            this._guid = guid.ToString() ?? throw new System.ArgumentNullException(nameof(guid));
        }
    }
}
")]
    [InlineData(@"
namespace Test
{
    [AutoConstructor]
    internal partial record Test
    {
        [AutoConstructorInject(parameterName: ""guid"")]
        private readonly string _guidString;
    }
}", @"namespace Test
{
    partial record Test
    {
        public Test(string guid)
        {
            this._guidString = guid ?? throw new System.ArgumentNullException(nameof(guid));
        }
    }
}
")]
    public async Task Run_WithInjectAttribute_ShouldGenerateRecord(string code, string generated)
    {
        await VerifySourceGenerator.RunAsync(code, generated);
    }

    [Theory]
    [InlineData(@"
namespace Test
{
    [AutoConstructor]
    internal partial record Test
    {
    }
}")]
    [InlineData(@"
namespace Test
{
    [AutoConstructor]
    internal partial record Test
    {
        [AutoConstructorIgnore]
        private readonly int _ignore;
    }
}")]
    [InlineData(@"
namespace Test
{
    [AutoConstructor]
    internal partial record Test
    {
        private readonly int _ignore = 0;
    }
}")]
    [InlineData(@"
namespace Test
{
    [AutoConstructor]
    internal partial record Test
    {
        private int _ignore;
    }
}")]
    public async Task Run_NoFieldsToInject_ShouldNotGenerateRecord(string code)
    {
        await VerifySourceGenerator.RunAsync(code);
    }

    [Fact]
    public async Task Run_WithAttributeAndWithoutPartial_ShouldNotGenerateRecord()
    {
        const string code = @"
namespace Test
{
    [AutoConstructor]
    internal record Test
    {
        private readonly int _t;
    }
}";

        await VerifySourceGenerator.RunAsync(code);
    }

    [Fact]
    public async Task Run_RecordWithoutNamespace_ShouldGenerateRecord()
    {
        const string code = @"
[AutoConstructor]
internal partial record Test
{
    private readonly int _t;
}";
        const string generated = @"partial record Test
{
    public Test(int t)
    {
        this._t = t;
    }
}
";

        await VerifySourceGenerator.RunAsync(code, generated, generatedName: "Test.g.cs");
    }

    [Theory]
    [InlineData("t")]
    [InlineData("_t")]
    [InlineData("__t")]
    public async Task Run_IdentifierWithOrWithoutUnderscore_ShouldGenerateSameRecord(string identifier)
    {
        string code = $@"
namespace Test
{{
    [AutoConstructor]
    internal partial record Test
    {{
        private readonly int {identifier};
    }}
}}";
        string generated = $@"namespace Test
{{
    partial record Test
    {{
        public Test(int t)
        {{
            this.{identifier} = t;
        }}
    }}
}}
";
        await VerifySourceGenerator.RunAsync(code, generated);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Run_WithMsbuildConfigNullChecks_ShouldGenerateRecord(bool disableNullChecks)
    {
        const string code = @"
namespace Test
{
    [AutoConstructor]
    internal partial record Test
    {
        private readonly string _t;
    }
}";
        string generated = $@"namespace Test
{{
    partial record Test
    {{
        public Test(string t)
        {{
            this._t = t{(!disableNullChecks ? " ?? throw new System.ArgumentNullException(nameof(t))" : "")};
        }}
    }}
}}
";

        await VerifySourceGenerator.RunAsync(code, generated, configFileContent: $"build_property.AutoConstructor_DisableNullChecking = {disableNullChecks}");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Run_WithMsbuildConfigGenerateDocumentation_ShouldGenerateRecord(bool generateDocumentation)
    {
        const string code = @"
namespace Test
{
    [AutoConstructor]
    internal partial record Test
    {
        /// <summary>
        /// Some field.
        /// </summary>
        private readonly string _t1;
        private readonly string _t2;
    }
}";

        string generated = @"namespace Test
{
    partial record Test
    {
        public Test(string t1, string t2)
        {
            this._t1 = t1 ?? throw new System.ArgumentNullException(nameof(t1));
            this._t2 = t2 ?? throw new System.ArgumentNullException(nameof(t2));
        }
    }
}
";

        if (generateDocumentation)
        {
            generated = @"namespace Test
{
    partial record Test
    {
        /// <summary>
        /// Initializes a new instance of the Test record.
        /// </summary>
        /// <param name=""t1"">Some field.</param>
        /// <param name=""t2"">t2</param>
        public Test(string t1, string t2)
        {
            this._t1 = t1 ?? throw new System.ArgumentNullException(nameof(t1));
            this._t2 = t2 ?? throw new System.ArgumentNullException(nameof(t2));
        }
    }
}
";
        }

        await VerifySourceGenerator.RunAsync(
            code,
            generated,
            configFileContent: $"build_property.AutoConstructor_GenerateConstructorDocumentation = {generateDocumentation}");
    }

    [Theory]
    [InlineData(false, "")]
    [InlineData(true, "Record {0} comment")]
    [InlineData(true, "")]
    public async Task Run_WithMsbuildConfigGenerateDocumentationWithCustomComment_ShouldGenerateRecord(bool hasCustomComment, string commentConfig)
    {
        const string code = @"
namespace Test
{
    [AutoConstructor]
    internal partial record Test
    {
        /// <summary>
        /// Some field.
        /// </summary>
        private readonly string _t1;
        private readonly string _t2;
    }
}";

        string comment = string.Format(CultureInfo.InvariantCulture, commentConfig, "Test");
        if (string.IsNullOrWhiteSpace(comment))
        {
            comment = "Initializes a new instance of the Test record.";
        }
        string generated = $@"namespace Test
{{
    partial record Test
    {{
        /// <summary>
        /// {comment}
        /// </summary>
        /// <param name=""t1"">Some field.</param>
        /// <param name=""t2"">t2</param>
        public Test(string t1, string t2)
        {{
            this._t1 = t1 ?? throw new System.ArgumentNullException(nameof(t1));
            this._t2 = t2 ?? throw new System.ArgumentNullException(nameof(t2));
        }}
    }}
}}
";

        string configFileContent = @"
build_property.AutoConstructor_GenerateConstructorDocumentation = true
";

        if (hasCustomComment)
        {
            configFileContent = $@"
build_property.AutoConstructor_GenerateConstructorDocumentation = true
build_property.AutoConstructor_ConstructorDocumentationComment = {commentConfig}
";
        }

        await VerifySourceGenerator.RunAsync(code, generated, configFileContent: configFileContent);
    }

    [Fact]
    public async Task Run_WithMismatchingTypes_ShouldNotGenerateRecord()
    {
        const string code = @"
namespace Test
{
    [AutoConstructor]
    internal partial record Test
    {
        [AutoConstructorInject(""guid.ToString()"", ""guid"", typeof(System.Guid))]
        private readonly string _i;
        private readonly string _guid;
    }
}";

        DiagnosticResult diagnosticResult = new DiagnosticResult(DiagnosticDescriptors.MistmatchTypesDiagnosticId, DiagnosticSeverity.Error).WithSpan(4, 5, 10, 6);
        await VerifySourceGenerator.RunAsync(code, diagnostics: new[] { diagnosticResult });
    }

    [Fact]
    public async Task Run_WithMismatchingFallbackTypes_ShouldNotGenerateRecord()
    {
        const string code = @"
namespace Test
{
    [AutoConstructor]
    internal partial record Test
    {
        [AutoConstructorInject(null, ""guid"", null)]
        private readonly string _i;

        [AutoConstructorInject(null, ""guid"", null)]
        private readonly System.Guid _guid;
    }
}";

        DiagnosticResult diagnosticResult = new DiagnosticResult(DiagnosticDescriptors.MistmatchTypesDiagnosticId, DiagnosticSeverity.Error).WithSpan(4, 5, 12, 6);
        await VerifySourceGenerator.RunAsync(code, diagnostics: new[] { diagnosticResult });
    }

    [Fact]
    public async Task Run_WithAliasForAttribute_ShouldGenerateRecord()
    {
        const string code = @"using Alias = AutoConstructorAttribute;
namespace Test
{
    [Alias]
    internal partial record Test
    {
        private readonly int _t;
    }
}";
        const string generated = @"namespace Test
{
    partial record Test
    {
        public Test(int t)
        {
            this._t = t;
        }
    }
}
";
        await VerifySourceGenerator.RunAsync(code, generated);
    }

    [Theory]
    [InlineData(@"namespace Test
{
    [AutoConstructor]
    internal partial record Test
    {
        private readonly string? _t1;
        private readonly string _t2;
        private readonly int _d1;
        private readonly int? _d2;
    }
}", @"#nullable enable
namespace Test
{
    partial record Test
    {
        public Test(string? t1, string t2, int d1, int? d2)
        {
            this._t1 = t1;
            this._t2 = t2 ?? throw new System.ArgumentNullException(nameof(t2));
            this._d1 = d1;
            this._d2 = d2;
        }
    }
}
")]
    [InlineData(@"using System.Threading.Tasks;namespace Test
{
    [AutoConstructor]
    internal partial record Test
    {
        private readonly Task<object?> _t1;
    }
}", @"#nullable enable
namespace Test
{
    partial record Test
    {
        public Test(System.Threading.Tasks.Task<object?> t1)
        {
            this._t1 = t1 ?? throw new System.ArgumentNullException(nameof(t1));
        }
    }
}
")]
    [InlineData(@"using System.Threading.Tasks;namespace Test
{
    [AutoConstructor]
    internal partial record Test
    {
        private readonly Task<Task<object?>> _t1;
    }
}", @"#nullable enable
namespace Test
{
    partial record Test
    {
        public Test(System.Threading.Tasks.Task<System.Threading.Tasks.Task<object?>> t1)
        {
            this._t1 = t1 ?? throw new System.ArgumentNullException(nameof(t1));
        }
    }
}
")]
    [InlineData(@"using System.Threading.Tasks;namespace Test
{
    [AutoConstructor]
    internal partial record Test : TestBase
    {
        
    }

    internal partial record TestBase
    {
        public readonly Task<Task<object?>> _t1;

        public TestBase(System.Threading.Tasks.Task<System.Threading.Tasks.Task<object?>> t1)
        {
            this._t1 = t1 ?? throw new System.ArgumentNullException(nameof(t1));
        }
    }
}", @"#nullable enable
namespace Test
{
    partial record Test
    {
        public Test(System.Threading.Tasks.Task<System.Threading.Tasks.Task<object?>> t1) : base(t1)
        {
        }
    }
}
")]
    public async Task Run_WithNullableReferenceType_ShouldGenerateRecord(string code, string generated)
    {
        await VerifySourceGenerator.RunAsync(code, generated, nullable: true);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Run_WithOrWithoutNullableReferenceType_ShouldGenerateRecordWithNullCheck(bool enableBoolean)
    {
        const string code = @"namespace Test
{
    [AutoConstructor]
    internal partial record Test
    {
        private readonly string _t;
    }
}";
        const string generated = @"namespace Test
{
    partial record Test
    {
        public Test(string t)
        {
            this._t = t ?? throw new System.ArgumentNullException(nameof(t));
        }
    }
}
";
        await VerifySourceGenerator.RunAsync(code, generated, nullable: enableBoolean);
    }

    [Theory]
    [InlineData(@"
namespace Nested
{
    internal partial record Outer
    {
        [AutoConstructor]
        internal partial record Inner
        {
            private readonly int _t;
        }
    }
}", @"namespace Nested
{
    partial record Outer
    {
        partial record Inner
        {
            public Inner(int t)
            {
                this._t = t;
            }
        }
    }
}
", "Nested.Outer.Inner.g.cs")]
    [InlineData(@"
internal partial record Outer
{
    [AutoConstructor]
    internal partial record Inner
    {
        private readonly int _t;
    }
}", @"partial record Outer
{
    partial record Inner
    {
        public Inner(int t)
        {
            this._t = t;
        }
    }
}
", "Outer.Inner.g.cs")]
    [InlineData(@"
internal partial record Outer1
{
    internal partial record Outer2
    {
        [AutoConstructor]
        internal partial record Inner
        {
            private readonly int _t;
        }
    }
}", @"partial record Outer1
{
    partial record Outer2
    {
        partial record Inner
        {
            public Inner(int t)
            {
                this._t = t;
            }
        }
    }
}
", "Outer1.Outer2.Inner.g.cs")]
    public async Task Run_WithNestedRecord_ShouldGenerateRecord(string code, string generated, string generatedName)
    {
        await VerifySourceGenerator.RunAsync(code, generated, generatedName: generatedName);
    }

    [Fact]
    public async Task Run_WithInjectAttributeOnProperties_ShouldGenerateRecord()
    {
        const string code = @"
namespace Test
{
    [AutoConstructor]
    internal partial record Test
    {
        [field: AutoConstructorInject]
        public int Injected { get; }

        /// <summary>
        /// Some property.
        /// </summary>
        [field: AutoConstructorInject]
        public int InjectedWithDocumentation { get; }

        [field: AutoConstructorInject]
        public int InjectedBecauseExplicitInjection { get; set; }

        [field: AutoConstructorInject]
        public static int NotInjectedBecauseStatic { get; }

        [field: AutoConstructorInject]
        public int NotInjectedBecauseInitialized { get; } = 2;

        public int AlsoInjectedEvenWhenMissingAttribute { get; }

        [field: AutoConstructorIgnore]
        public int NotInjectedBecauseHasIgnoreAttribute { get; }

        [field: AutoConstructorInject(initializer: ""injected.ToString()"", injectedType: typeof(int), parameterName: ""injected"")]
        public string InjectedWithoutCreatingAParam { get; }
    }
}";
        const string generated = @"namespace Test
{
    partial record Test
    {
        /// <summary>
        /// Initializes a new instance of the Test record.
        /// </summary>
        /// <param name=""injected"">injected</param>
        /// <param name=""injectedWithDocumentation"">Some property.</param>
        /// <param name=""injectedBecauseExplicitInjection"">injectedBecauseExplicitInjection</param>
        /// <param name=""alsoInjectedEvenWhenMissingAttribute"">alsoInjectedEvenWhenMissingAttribute</param>
        public Test(int injected, int injectedWithDocumentation, int injectedBecauseExplicitInjection, int alsoInjectedEvenWhenMissingAttribute)
        {
            this.Injected = injected;
            this.InjectedWithDocumentation = injectedWithDocumentation;
            this.InjectedBecauseExplicitInjection = injectedBecauseExplicitInjection;
            this.AlsoInjectedEvenWhenMissingAttribute = alsoInjectedEvenWhenMissingAttribute;
            this.InjectedWithoutCreatingAParam = injected.ToString() ?? throw new System.ArgumentNullException(nameof(injected));
        }
    }
}
";

        await VerifySourceGenerator.RunAsync(code, generated, configFileContent: "build_property.AutoConstructor_GenerateConstructorDocumentation = true");
    }

    [Fact]
    public async Task Run_WithRecordLikeRecord_ShouldGenerateRecord()
    {
        const string code = @"
namespace Test
{
    [AutoConstructor]
    public partial record Test
    {
        public string Name { get; }
        public string LastName { get; }
    }
}";
        const string generated = @"namespace Test
{
    partial record Test
    {
        public Test(string name, string lastName)
        {
            this.Name = name ?? throw new System.ArgumentNullException(nameof(name));
            this.LastName = lastName ?? throw new System.ArgumentNullException(nameof(lastName));
        }
    }
}
";

        await VerifySourceGenerator.RunAsync(code, generated);
    }

    [Fact]
    public async Task Run_AllKindsOfFields_ShouldGenerateRecord()
    {
        const string code = @"
namespace Test
{
    [AutoConstructor]
    internal partial record Test
    {
        private readonly int _t1;
        public readonly int _t2;
        protected readonly int _t3;
        private static readonly int _t4;
        [AutoConstructorInject]
        private int _t5;
    }
}";
        const string generated = @"namespace Test
{
    partial record Test
    {
        public Test(int t1, int t2, int t3)
        {
            this._t1 = t1;
            this._t2 = t2;
            this._t3 = t3;
        }
    }
}
";
        await VerifySourceGenerator.RunAsync(code, generated);
    }

    [Fact]
    public async Task Run_OnInheritedWithStaticImplicitConstructor_ShouldGenerateBaseCall()
    {
        const string code = @"
namespace Test
{
    internal record TestBase
    {
        private static readonly int _s = 1;

        private readonly int _t1;

        public TestBase(int t1)
        {
            this._t1 = t1;
        }
    }

    [AutoConstructor]
    internal partial record Test : TestBase
    {
    }
}";
        const string generated = @"namespace Test
{
    partial record Test
    {
        public Test(int t1) : base(t1)
        {
        }
    }
}
";
        await VerifySourceGenerator.RunAsync(code, generated);
    }

    [Fact]
    public async Task Run_MultiplePartialParts_ShouldGenerateRecord()
    {
        const string code = @"
namespace Test
{
    [AutoConstructor]
    internal partial record Test
    {
        private readonly int _i1;
    }

    internal partial record Test
    {
        private readonly int _i2;
    }
}";
        const string generated = @"namespace Test
{
    partial record Test
    {
        public Test(int i1, int i2)
        {
            this._i1 = i1;
            this._i2 = i2;
        }
    }
}
";
        await VerifySourceGenerator.RunAsync(code, generated);
    }

    [Fact]
    public async Task Run_WithMismatchingTypesWithTwoPartialParts_ShouldReportDiagnosticOnEachPart()
    {
        const string code = @"
namespace Test
{
    [AutoConstructor]
    internal partial record Test
    {
        [AutoConstructorInject(""guid.ToString()"", ""guid"", typeof(System.Guid))]
        private readonly string _i;
    }

    internal partial record Test
    {
        private readonly string _guid;
    }
}";

        DiagnosticResult diagnosticResultFirstPart = new DiagnosticResult(DiagnosticDescriptors.MistmatchTypesDiagnosticId, DiagnosticSeverity.Error).WithSpan(4, 5, 9, 6);
        DiagnosticResult diagnosticResultSecondPart = new DiagnosticResult(DiagnosticDescriptors.MistmatchTypesDiagnosticId, DiagnosticSeverity.Error).WithSpan(11, 5, 14, 6);
        await VerifySourceGenerator.RunAsync(code, diagnostics: new[] { diagnosticResultFirstPart, diagnosticResultSecondPart });
    }

    [Theory]
    [InlineData(@"
namespace Test
{
    [AutoConstructor]
    internal partial record Test<T>
    {
        private readonly T _generic;
    }
}", @"namespace Test
{
    partial record Test<T>
    {
        public Test(T generic)
        {
            this._generic = generic;
        }
    }
}
")]
    [InlineData(@"
namespace Test
{
    [AutoConstructor]
    internal partial record Test<T1, T2>
    {
        private readonly T1 _generic1;
        private readonly T2 _generic2;
    }
}", @"namespace Test
{
    partial record Test<T1, T2>
    {
        public Test(T1 generic1, T2 generic2)
        {
            this._generic1 = generic1;
            this._generic2 = generic2;
        }
    }
}
", "Test.Test.T1.T2.g.cs")]
    [InlineData(@"
namespace Test
{
    [AutoConstructor]
    internal partial record Test<T> where T : class
    {
        private readonly T _generic;
    }
}", @"namespace Test
{
    partial record Test<T>
    {
        public Test(T generic)
        {
            this._generic = generic ?? throw new System.ArgumentNullException(nameof(generic));
        }
    }
}
")]
    [InlineData(@"
namespace Nested
{
    internal partial record Outer<T>
    {
        [AutoConstructor]
        internal partial record Inner
        {
            private readonly T _t;
        }
    }
}", @"namespace Nested
{
    partial record Outer<T>
    {
        partial record Inner
        {
            public Inner(T t)
            {
                this._t = t;
            }
        }
    }
}
", "Nested.Outer.Inner.g.cs")]
    [InlineData(@"
namespace Nested
{
    internal partial record Outer<T1>
    {
        [AutoConstructor]
        internal partial record Inner<T2>
        {
            private readonly T1 _t1;
            private readonly T2 _t2;
        }
    }
}", @"namespace Nested
{
    partial record Outer<T1>
    {
        partial record Inner<T2>
        {
            public Inner(T1 t1, T2 t2)
            {
                this._t1 = t1;
                this._t2 = t2;
            }
        }
    }
}
", "Nested.Outer.Inner.T2.g.cs")]
    [InlineData(@"
namespace Test
{
    interface IThing<T> {}

    [AutoConstructor]
    internal partial record Test<T>
    {
        private readonly IThing<T> _generic;
    }
}", @"namespace Test
{
    partial record Test<T>
    {
        public Test(Test.IThing<T> generic)
        {
            this._generic = generic ?? throw new System.ArgumentNullException(nameof(generic));
        }
    }
}
")]
    public async Task Run_WithGenericRecord_ShouldGenerateRecord(string code, string generated, string generatedName = "Test.Test.T.g.cs")
    {
        await VerifySourceGenerator.RunAsync(code, generated, generatedName: generatedName);
    }

    [Fact]
    public async Task Run_WithBasicInheritance_ShouldGenerateRecord()
    {
        const string code = @"
namespace Test
{
    internal record BaseRecord
    {
        private readonly int _t;
        public BaseRecord(int t)
        {
            this._t = t;
        }
    }
    [AutoConstructor]
    internal partial record Test : BaseRecord
    {
    }
}";
        const string generated = @"namespace Test
{
    partial record Test
    {
        public Test(int t) : base(t)
        {
        }
    }
}
";
        await VerifySourceGenerator.RunAsync(code, generated);
    }

    [Theory]
    [InlineData(@"
namespace Test
{
    internal record BaseRecord
    {
        private readonly int _t;
        private readonly System.Guid _guid;
        public BaseRecord(System.Guid guid, int t)
        {
            this._t = t;
            this._guid = guid;
        }
    }
    [AutoConstructor]
    internal partial record Test : BaseRecord
    {
        [AutoConstructorInject(parameterName: ""guid"")]
        private readonly System.Guid _guid2;
    }
}", @"namespace Test
{
    partial record Test
    {
        public Test(System.Guid guid, int t) : base(guid, t)
        {
            this._guid2 = guid;
        }
    }
}
")]
    [InlineData(@"
namespace Test
{
    internal record BaseRecord
    {
        private readonly int _t;
        public BaseRecord(int t)
        {
            this._t = t;
        }
    }
    [AutoConstructor]
    internal partial record Test : BaseRecord
    {
        private readonly System.Guid _guid;
    }
}", @"namespace Test
{
    partial record Test
    {
        public Test(System.Guid guid, int t) : base(t)
        {
            this._guid = guid;
        }
    }
}
")]
    [InlineData(@"
namespace Test
{
    internal record UpperRecord
    {
        private readonly System.DateTime _date;
        public UpperRecord(System.DateTime date)
        {
            this._date = date;
        }
    }
    internal record BaseRecord : UpperRecord
    {
        private readonly int _t;
        public BaseRecord(System.DateTime date, int t) : base(date)
        {
            this._t = t;
        }
    }
    [AutoConstructor]
    internal partial record Test : BaseRecord
    {
        private readonly System.Guid _guid;
    }
}", @"namespace Test
{
    partial record Test
    {
        public Test(System.Guid guid, System.DateTime date, int t) : base(date, t)
        {
            this._guid = guid;
        }
    }
}
")]
    public async Task Run_WithInheritanceAndFieldsInBothRecords_ShouldGenerateRecord(string code, string generated)
    {
        await VerifySourceGenerator.RunAsync(code, generated);
    }

    [Fact]
    public async Task Run_WithInheritanceAlsoGenerated_ShouldGenerateRecord()
    {
        const string code = @"
namespace Test
{
    [AutoConstructor]
    internal partial record BaseRecord
    {
        private readonly int _t;
    }
    [AutoConstructor]
    internal partial record Test : BaseRecord
    {
    }
}";
        const string generatedTest = @"namespace Test
{
    partial record Test
    {
        public Test(int t) : base(t)
        {
        }
    }
}
";

        const string generatedBase = @"namespace Test
{
    partial record BaseRecord
    {
        public BaseRecord(int t)
        {
            this._t = t;
        }
    }
}
";
        await VerifySourceGenerator.RunAsync(code, new[] { (generatedBase, "Test.BaseRecord.g.cs"), (generatedTest, "Test.Test.g.cs") });
    }

    [Fact]
    public async Task Run_WithMultipleInheritanceAlsoGenerated_ShouldGenerateRecord()
    {
        const string code = @"
namespace Test
{
    [AutoConstructor]
    internal partial record MotherRecord
    {
        private readonly string _s;
    }
    [AutoConstructor]
    internal partial record BaseRecord : MotherRecord
    {
        private readonly int _t;
    }
    [AutoConstructor]
    internal partial record Test : BaseRecord
    {
    }
}";
        const string generatedTest = @"namespace Test
{
    partial record Test
    {
        public Test(int t, string s) : base(t, s)
        {
        }
    }
}
";

        const string generatedBase = @"namespace Test
{
    partial record BaseRecord
    {
        public BaseRecord(int t, string s) : base(s)
        {
            this._t = t;
        }
    }
}
";

        const string generatedMother = @"namespace Test
{
    partial record MotherRecord
    {
        public MotherRecord(string s)
        {
            this._s = s ?? throw new System.ArgumentNullException(nameof(s));
        }
    }
}
";
        await VerifySourceGenerator.RunAsync(code, new[]
        {
            (generatedMother, "Test.MotherRecord.g.cs"),
            (generatedBase, "Test.BaseRecord.g.cs"),
            (generatedTest, "Test.Test.g.cs")
        });
    }

    [Fact]
    public async Task Run_WithMultipleInheritanceGeneratedAndNotGenerated_ShouldGenerateRecord()
    {
        const string code = @"
namespace Test
{
    internal record Record1
    {
        private readonly string _s;
        public Record1(string s)
        {
            this._s = s ?? throw new System.ArgumentNullException(nameof(s));
        }
    }
    [AutoConstructor]
    internal partial record Record2 : Record1
    {
        private readonly int _t;
    }
    [AutoConstructor]
    internal partial record Record3 : Record2
    {
    }
    internal record Record4 : Record3
    {
        public Record4(int t, string s) : base(t, s)
        {
        }
    }
    [AutoConstructor]
    internal partial record Record5 : Record4
    {
    }
}";
        const string generatedRecord3 = @"namespace Test
{
    partial record Record3
    {
        public Record3(int t, string s) : base(t, s)
        {
        }
    }
}
";

        const string generatedRecord2 = @"namespace Test
{
    partial record Record2
    {
        public Record2(int t, string s) : base(s)
        {
            this._t = t;
        }
    }
}
";

        const string generatedRecord5 = @"namespace Test
{
    partial record Record5
    {
        public Record5(int t, string s) : base(t, s)
        {
        }
    }
}
";
        await VerifySourceGenerator.RunAsync(code, new[]
        {
            (generatedRecord2, "Test.Record2.g.cs"),
            (generatedRecord3, "Test.Record3.g.cs"),
            (generatedRecord5, "Test.Record5.g.cs")
        });
    }
}
