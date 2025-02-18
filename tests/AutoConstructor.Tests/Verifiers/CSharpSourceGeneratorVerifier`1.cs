using System.Text;
using AutoConstructor.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;
using Microsoft.CodeAnalysis.Text;

namespace AutoConstructor.Tests.Verifiers;

internal static class CSharpSourceGeneratorVerifier<TSourceGenerator>
    where TSourceGenerator : IIncrementalGenerator, new()
{
    public static Task RunAsync(
        string code,
        string generated = "",
        string generatedName = "Test.Test.g.cs",
        bool nullable = false,
        IEnumerable<DiagnosticResult>? diagnostics = null,
        string? configFileContent = null)
    {
        return RunAsync(code, new[] { (generated, generatedName) }, nullable, diagnostics, configFileContent);
    }

    public static async Task RunAsync(
        string code,
        (string, string)[] generatedSources,
        bool nullable = false,
        IEnumerable<DiagnosticResult>? diagnostics = null,
        string? configFileContent = null)
    {
        var test = new CSharpSourceGeneratorVerifier<TSourceGenerator>.Test()
        {
            TestState =
                {
                    Sources = { code },
                    GeneratedSources =
                    {
                        (typeof(AutoConstructorGenerator), "AutoConstructorAttribute.cs", SourceText.From(Source.AttributeText, Encoding.UTF8)),
                        (typeof(AutoConstructorGenerator), "AutoConstructorIgnoreAttribute.cs", SourceText.From(Source.IgnoreAttributeText, Encoding.UTF8)),
                        (typeof(AutoConstructorGenerator), "AutoConstructorInjectAttribute.cs", SourceText.From(Source.InjectAttributeText, Encoding.UTF8)),
                    }
                },
            EnableNullable = nullable,
            LanguageVersion = LanguageVersion.Default,
        };

        foreach ((string? generated, string generatedName) in generatedSources)
        {
            if (generated is string { Length: > 0 })
            {
                string generatedWithHeader = @$"//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by the {nameof(AutoConstructor)} source generator.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------
{generated}";
                test.TestState.GeneratedSources.Add((typeof(AutoConstructorGenerator), generatedName, SourceText.From(generatedWithHeader, Encoding.UTF8)));
            }
        }

        if (diagnostics is not null)
        {
            test.TestState.ExpectedDiagnostics.AddRange(diagnostics);
        }

        // Enable null checks for the tests.
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", SourceText.From($@"
is_global=true
build_property.AutoConstructor_DisableNullChecking = false
{configFileContent}")));

        await test.RunAsync();
    }

    private sealed class Test : CSharpSourceGeneratorTest<EmptySourceGeneratorProvider, XUnitVerifier>
    {
        public bool EnableNullable { get; set; }

        public LanguageVersion LanguageVersion { get; set; }

        protected override IEnumerable<ISourceGenerator> GetSourceGenerators()
        {
            yield return new TSourceGenerator().AsSourceGenerator();
        }

        protected override CompilationOptions CreateCompilationOptions()
        {
            if (base.CreateCompilationOptions() is not CSharpCompilationOptions compilationOptions)
            {
                throw new InvalidOperationException("Invalid compilation options");
            }

            if (EnableNullable)
            {
                compilationOptions = compilationOptions.WithNullableContextOptions(NullableContextOptions.Enable);
            }

            return compilationOptions
                .WithSpecificDiagnosticOptions(compilationOptions.SpecificDiagnosticOptions.SetItems(CSharpVerifierHelper.NullableWarnings));
        }

        protected override ParseOptions CreateParseOptions()
        {
            return ((CSharpParseOptions)base.CreateParseOptions()).WithLanguageVersion(LanguageVersion);
        }
    }
}
