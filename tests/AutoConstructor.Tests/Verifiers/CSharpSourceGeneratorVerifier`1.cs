using System.Text;
using System.Text.RegularExpressions;
using AutoConstructor.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;

namespace AutoConstructor.Tests.Verifiers;

internal enum ConstructorType
{
    WithParametersOnly = 1,
    ParameterlessOnly = 2,
    Both = 3
}

internal static partial class CSharpSourceGeneratorVerifier<TSourceGenerator>
    where TSourceGenerator : IIncrementalGenerator, new()
{
    public static Task RunAsync(
        string code,
        string generated = "",
        string generatedName = "Test.Test.g.cs",
        bool nullable = false,
        IEnumerable<DiagnosticResult>? diagnostics = null,
        string? configFileContent = null,
        string? additionalProjectsSource = null,
        bool runWithNullChecks = true,
        ConstructorType expectedConstructors = ConstructorType.WithParametersOnly,
        string? expectedObsoleteMessage = "")
    {
        return RunAsync(code, new[] { (generated, generatedName) }, nullable, diagnostics, configFileContent, additionalProjectsSource, runWithNullChecks, expectedConstructors, expectedObsoleteMessage);
    }

    public static async Task RunAsync(
        string code,
        (string, string)[] generatedSources,
        bool nullable = false,
        IEnumerable<DiagnosticResult>? diagnostics = null,
        string? configFileContent = null,
        string? additionalProjectsSource = null,
        bool runWithNullChecks = true,
        ConstructorType expectedConstructors = ConstructorType.WithParametersOnly,
        string? expectedObsoleteMessage = "")
    {
        var test = new CSharpSourceGeneratorVerifier<TSourceGenerator>.Test()
        {
            TestState =
                {
                    Sources = { code },
                    GeneratedSources =
                    {
                        (typeof(AutoConstructorGenerator), $"{Source.AttributeFullName}.cs", SourceText.From(Source.AttributeText, Encoding.UTF8)),
                        (typeof(AutoConstructorGenerator), $"{Source.IgnoreAttributeFullName}.cs", SourceText.From(Source.IgnoreAttributeText, Encoding.UTF8)),
                        (typeof(AutoConstructorGenerator), $"{Source.InjectAttributeFullName}.cs", SourceText.From(Source.InjectAttributeText, Encoding.UTF8)),
                        (typeof(AutoConstructorGenerator), $"{Source.InitializerAttributeFullName}.cs", SourceText.From(Source.InitializerAttributeText, Encoding.UTF8)),
                        (typeof(AutoConstructorGenerator), $"{Source.DefaultBaseAttributeFullName}.cs", SourceText.From(Source.DefaultBaseAttributeText, Encoding.UTF8)),
                    },
                    AdditionalProjects =
                    {
                        ["DependencyProject"] = { },
                    },
                },
            EnableNullable = nullable,
            LanguageVersion = LanguageVersion.Default,
        };

        if (additionalProjectsSource is not null)
        {
            test.TestState.AdditionalProjectReferences.Add("DependencyProject");
            test.TestState.AdditionalProjects["DependencyProject"].Sources.Add(additionalProjectsSource);
        }

        foreach ((string? generated, string generatedName) in generatedSources)
        {
            string header = @$"//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by the {nameof(AutoConstructor)} source generator.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------";

            if (generated is string { Length: > 0 })
            {
                var matches = BeforeConstructorRegex().Matches(generated).ToList();
                if (matches.Count == 0)
                {
                    continue;
                }
                var sb = new StringBuilder();
                sb.AppendLine(header);

                int lastIndex = 0;
                for (int i = 0; i < matches.Count; i++)
                {
                    Match match = matches[i];
                    // Append text before the current match
                    sb.Append(generated.AsSpan(lastIndex, match.Index - lastIndex));
                    bool expectedParameterless = expectedConstructors == ConstructorType.ParameterlessOnly || (expectedConstructors == ConstructorType.Both && i == 1);
                    bool isFirstConstructor = i == 0;
                    string constructorCode = FormatConstructor(match, isFirstConstructor, expectedParameterless, expectedObsoleteMessage);
                    sb.Append(constructorCode);
                    lastIndex = match.Index + match.Length;
                }

                // Append the remaining part of the generated code
                sb.Append(generated.AsSpan(lastIndex));
                string generatedWithHeader = sb.ToString().Replace("\r\n", "\n", StringComparison.OrdinalIgnoreCase).TrimEnd();
                test.TestState.GeneratedSources.Add((typeof(AutoConstructorGenerator), generatedName, SourceText.From(generatedWithHeader, Encoding.UTF8)));
            }
        }

        if (diagnostics is not null)
        {
            test.TestState.ExpectedDiagnostics.AddRange(diagnostics);
        }

        // Enable null checks for the tests.
        if (runWithNullChecks)
        {
            configFileContent = $@"
build_property.{BuildProperties.AutoConstructor_GenerateArgumentNullExceptionChecks} = true
{configFileContent}
";
        }

        test.TestState.AnalyzerConfigFiles.Add(("/.globalconfig", SourceText.From($@"
is_global=true
{configFileContent}")));

        await test.RunAsync();
    }

    private static string FormatConstructor(Match match, bool isFirstConstructor, bool expectedParameterlessConstructor, string? expectedObsoleteMessage)
    {
        string beforeCtorWhitespace = $"\n{match.Groups[1].Value.Replace("\r\n", "", StringComparison.OrdinalIgnoreCase)}";
        var lines = new List<string>();
        if (!isFirstConstructor)
        {
            //separator line between the two constructors, this is not captured by any ConstructorRegex
            lines.Add("");
        }
        lines.Add(@$"[global::System.CodeDom.Compiler.GeneratedCodeAttribute(""{nameof(AutoConstructor)}"", ""{AutoConstructorGenerator.GeneratorVersion}"")]");

        if (expectedParameterlessConstructor)
        {
            lines.Add("#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor.");
            if (expectedObsoleteMessage is not null)
            {
                string obsoleteMessage = string.IsNullOrWhiteSpace(expectedObsoleteMessage) ? "For serialization only." : expectedObsoleteMessage;
                lines.Add($@"[global::System.ObsoleteAttribute(""{obsoleteMessage}"", true)]");
            }
        }
        string beforeCtorBoilerPlate = string.Concat(lines.Select(d => beforeCtorWhitespace + d));
        return beforeCtorBoilerPlate + match.Value;
    }

    private sealed class Test : CSharpSourceGeneratorTest<EmptySourceGeneratorProvider, DefaultVerifier>
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

    [GeneratedRegex("(\\s+)(public|internal|private|protected|protected internal|private protected) (\\w+)\\(")]
    private static partial Regex BeforeConstructorRegex();
}
