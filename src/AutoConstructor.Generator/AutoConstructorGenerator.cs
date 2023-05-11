using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Xml;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace AutoConstructor.Generator;

[Generator]
public class CombinedAutoConstructorGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Register the attribute source
        context.RegisterPostInitializationOutput((i) =>
        {
            i.AddSource(Source.AttributeFullName, SourceText.From(Source.AttributeText, Encoding.UTF8));
            i.AddSource(Source.IgnoreAttributeFullName, SourceText.From(Source.IgnoreAttributeText, Encoding.UTF8));
            i.AddSource(Source.InjectAttributeFullName, SourceText.From(Source.InjectAttributeText, Encoding.UTF8));
        });

        var classGenerator = new AutoConstructorGenerator<ClassDeclarationSyntax>(new ConstructorSelector(), () => new ClassGenerator());
        classGenerator.Initialize(context);
        var recordGenerator = new AutoConstructorGenerator<RecordDeclarationSyntax>(new ConstructorSelector(), () => new RecordGenerator());
        recordGenerator.Initialize(context);
    }
}

public class ConstructorSelector : IConstructorSelector
{
    public List<IMethodSymbol> SelectValidConstructors(INamedTypeSymbol baseType, Compilation compilation)
    {
        IEnumerable<IMethodSymbol> nonStatic = baseType.Constructors.Where(d => !d.IsStatic);
        //records have strange constructor which has single parameter of the record itself type
        return nonStatic.Where(d => !(d.IsImplicitlyDeclared && d.Parameters.Length == 1 && SymbolEqualityComparer.Default.Equals(d.Parameters[0].Type, baseType))).ToList();
    }
}

public interface IConstructorSelector
{
    List<IMethodSymbol> SelectValidConstructors(INamedTypeSymbol baseType, Compilation compilation);
}

public class AutoConstructorGenerator<TTarget> : IIncrementalGenerator where TTarget : TypeDeclarationSyntax
{
    private readonly IConstructorSelector _constructorSelector;
    private readonly Func<CodeGenerator<TTarget>> _createGenerator;

    public AutoConstructorGenerator(IConstructorSelector constructorSelector, Func<CodeGenerator<TTarget>> createGenerator)
    {
        _constructorSelector = constructorSelector;
        _createGenerator = createGenerator;
    }

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<TTarget> classDeclarations = context.SyntaxProvider
                .CreateSyntaxProvider(static (s, _) => IsSyntaxTargetForGeneration(s), static (ctx, _) => GetSemanticTargetForGeneration(ctx))
                .Where(static m => m is not null)!;

        IncrementalValueProvider<(Compilation compilation, ImmutableArray<TTarget> classes, AnalyzerConfigOptions options)> valueProvider =
            context.CompilationProvider
            .Combine(classDeclarations.Collect())
            .Combine(context.AnalyzerConfigOptionsProvider.Select((c, _) => c.GlobalOptions))
            .Select((c, _) => (compilation: c.Left.Left, classes: c.Left.Right, options: c.Right));

        context.RegisterSourceOutput(valueProvider, (spc, source) => Execute(source.compilation, source.classes, spc, source.options, _constructorSelector, _createGenerator));
    }

    private static bool IsSyntaxTargetForGeneration(SyntaxNode node)
    {
        return node is TTarget TTarget && TTarget.Modifiers.Any(SyntaxKind.PartialKeyword);
    }

    private static TTarget? GetSemanticTargetForGeneration(GeneratorSyntaxContext context)
    {
        var TTarget = (TTarget)context.Node;

        INamedTypeSymbol? symbol = context.SemanticModel.GetDeclaredSymbol(TTarget);

        return symbol?.HasAttribute(Source.AttributeFullName, context.SemanticModel.Compilation) is true ? TTarget : null;
    }

    private static void Execute(Compilation compilation, ImmutableArray<TTarget> classes, SourceProductionContext context, AnalyzerConfigOptions options, IConstructorSelector constructorSelector, Func<CodeGenerator<TTarget>> codeGenerator)
    {
        if (classes.IsDefaultOrEmpty)
        {
            return;
        }

        IEnumerable<IGrouping<ISymbol?, TTarget>> classesBySymbol = Enumerable.Empty<IGrouping<ISymbol?, TTarget>>();
        try
        {
            classesBySymbol = classes.GroupBy(c => compilation.GetSemanticModel(c.SyntaxTree).GetDeclaredSymbol(c), SymbolEqualityComparer.Default);
        }
        catch (ArgumentException)
        {
            return;
        }

        foreach (IGrouping<ISymbol?, TTarget> groupedClasses in classesBySymbol)
        {
            if (context.CancellationToken.IsCancellationRequested)
            {
                break;
            }

            INamedTypeSymbol? symbol = groupedClasses.Key as INamedTypeSymbol;
            if (symbol is not null)
            {
                string filename = string.Empty;

                if (symbol.ContainingType is not null)
                {
                    filename = $"{string.Join(".", symbol.GetContainingTypes().Select(c => c.Name))}.";
                }

                filename += $"{symbol.Name}";

                if (symbol.TypeArguments.Length > 0)
                {
                    filename += string.Concat(symbol.TypeArguments.Select(tp => $".{tp.Name}"));
                }

                if (!symbol.ContainingNamespace.IsGlobalNamespace)
                {
                    filename = $"{symbol.ContainingNamespace.ToDisplayString()}.{filename}";
                }

                filename += ".g.cs";

                bool emitNullChecks = false;
                if (options.TryGetValue("build_property.AutoConstructor_DisableNullChecking", out string? disableNullCheckingSwitch))
                {
                    emitNullChecks = disableNullCheckingSwitch.Equals("false", StringComparison.OrdinalIgnoreCase);
                }

                List<FieldInfo> concatenatedFields = GetFieldsFromSymbol(compilation, symbol, emitNullChecks);

                ExtractFieldsFromParent(compilation, symbol, emitNullChecks, concatenatedFields, constructorSelector);

                FieldInfo[] fields = concatenatedFields.ToArray();

                if (fields.Length == 0)
                {
                    // No need to report diagnostic, taken care by the analyzers.
                    continue;
                }

                if (fields.GroupBy(x => x.ParameterName).Any(g =>
                    g.Where(c => c.Type is not null).Select(c => c.Type).Distinct(SymbolEqualityComparer.Default).Count() > 1
                    || (g.All(c => c.Type is null) && g.Select(c => c.FallbackType).Distinct(SymbolEqualityComparer.Default).Count() > 1)
                    ))
                {
                    foreach (TTarget classDeclaration in groupedClasses)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.MistmatchTypesRule, classDeclaration.GetLocation()));
                    }

                    continue;
                }

                context.AddSource(filename, SourceText.From(GenerateAutoConstructor(symbol, fields, options, codeGenerator()), Encoding.UTF8));
            }
        }
    }

    private static string GenerateAutoConstructor(INamedTypeSymbol symbol, FieldInfo[] fields, AnalyzerConfigOptions options, CodeGenerator<TTarget> codeGenerator)
    {
        bool generateConstructorDocumentation = false;
        if (options.TryGetValue("build_property.AutoConstructor_GenerateConstructorDocumentation", out string? generateConstructorDocumentationSwitch))
        {
            generateConstructorDocumentation = generateConstructorDocumentationSwitch.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        options.TryGetValue("build_property.AutoConstructor_ConstructorDocumentationComment", out string? constructorDocumentationComment);
        if (string.IsNullOrWhiteSpace(constructorDocumentationComment))
        {
            constructorDocumentationComment = "Initializes a new instance of the {0} {1}.";
        }

        if (fields.Any(f => f.Nullable))
        {
            codeGenerator.AddNullableAnnotation();
        }

        if (generateConstructorDocumentation)
        {
            string typeTypeName = typeof(TTarget).Name.Replace("DeclarationSyntax", "").ToLowerInvariant();//record or class
            codeGenerator.AddDocumentation(string.Format(CultureInfo.InvariantCulture, constructorDocumentationComment, symbol.Name, typeTypeName));
        }

        if (!symbol.ContainingNamespace.IsGlobalNamespace)
        {
            codeGenerator.AddNamespace(symbol.ContainingNamespace);
        }

        foreach (INamedTypeSymbol containingType in symbol.GetContainingTypes())
        {
            codeGenerator.AddClass(containingType);
        }

        codeGenerator
            .AddClass(symbol)
            .AddConstructor(fields);

        return codeGenerator.ToString();
    }

    private static List<FieldInfo> GetFieldsFromSymbol(Compilation compilation, INamedTypeSymbol symbol, bool emitNullChecks)
    {
        return symbol.GetMembers().OfType<IFieldSymbol>()
            .Where(x => x.CanBeInjected(compilation)
                && !x.IsStatic
                && (x.IsReadOnly || IsPropertyWithExplicitInjection(x))
                && !x.IsInitialized()
                && !x.HasAttribute(Source.IgnoreAttributeFullName, compilation))
            .Select(x => GetFieldInfo(x, compilation, emitNullChecks))
            .ToList();

        bool IsPropertyWithExplicitInjection(IFieldSymbol x)
        {
            return x.AssociatedSymbol is not null && x.HasAttribute(Source.InjectAttributeFullName, compilation);
        }
    }

    private static FieldInfo GetFieldInfo(IFieldSymbol fieldSymbol, Compilation compilation, bool emitNullChecks)
    {
        ITypeSymbol type = fieldSymbol.Type;
        ITypeSymbol? injectedType = type;
        string parameterName = fieldSymbol.Name.TrimStart('_');
        if (fieldSymbol.AssociatedSymbol is not null)
        {
            parameterName = char.ToLowerInvariant(fieldSymbol.AssociatedSymbol.Name[0]) + fieldSymbol.AssociatedSymbol.Name.Substring(1);
        }

        string initializer = parameterName;
        string? documentationComment = (fieldSymbol.AssociatedSymbol ?? fieldSymbol).GetDocumentationCommentXml();
        string? summaryText = null;

        if (!string.IsNullOrWhiteSpace(documentationComment))
        {
            using var reader = new StringReader(documentationComment);
            var document = new XmlDocument();
            document.Load(reader);
            summaryText = document.SelectSingleNode("member/summary")?.InnerText.Trim();
        }

        AttributeData? attributeData = fieldSymbol.GetAttribute(Source.InjectAttributeFullName, compilation);
        if (attributeData is not null)
        {
            ImmutableArray<IParameterSymbol> parameters = attributeData.AttributeConstructor?.Parameters ?? ImmutableArray.Create<IParameterSymbol>();
            if (GetParameterValue<string>("parameterName", parameters, attributeData.ConstructorArguments) is string { Length: > 0 } parameterNameValue)
            {
                parameterName = parameterNameValue;
                initializer = parameterNameValue;
            }

            if (GetParameterValue<string>("initializer", parameters, attributeData.ConstructorArguments) is string { Length: > 0 } initializerValue)
            {
                initializer = initializerValue;
            }

            injectedType = GetParameterValue<INamedTypeSymbol>("injectedType", parameters, attributeData.ConstructorArguments);
        }

        return new FieldInfo(
            injectedType,
            parameterName,
            fieldSymbol.AssociatedSymbol?.Name ?? fieldSymbol.Name,
            initializer,
            type,
            IsNullable(type),
            summaryText,
            type.IsReferenceType && type.NullableAnnotation != NullableAnnotation.Annotated && emitNullChecks,
            FieldType.Initialized);
    }

    private static bool IsNullable(ITypeSymbol typeSymbol)
    {
        bool isNullable = typeSymbol.IsReferenceType && typeSymbol.NullableAnnotation == NullableAnnotation.Annotated;
        if (typeSymbol is INamedTypeSymbol namedSymbol)
        {
            isNullable |= namedSymbol.TypeArguments.Any(IsNullable);
        }

        return isNullable;
    }

    private static T? GetParameterValue<T>(string parameterName, ImmutableArray<IParameterSymbol> parameters, ImmutableArray<TypedConstant> arguments)
        where T : class
    {
        return parameters.ToList().FindIndex(c => c.Name == parameterName) is int index and not -1
            ? (arguments[index].Value as T)
            : null;
    }

    private static void ExtractFieldsFromParent(Compilation compilation, INamedTypeSymbol symbol, bool emitNullChecks, List<FieldInfo> concatenatedFields, IConstructorSelector constructorSelector)
    {
        INamedTypeSymbol? baseType = symbol.BaseType;

        // Check if base type is not object (ie. its base type is null) and select valid constructor to call

        if (baseType?.BaseType is not null)
        {
            List<IMethodSymbol> validConstructors = constructorSelector.SelectValidConstructors(baseType, compilation);
            if (validConstructors.Count == 1)
            {
                IMethodSymbol constructor = validConstructors.Single();
                if (baseType?.HasAttribute(Source.AttributeFullName, compilation) is true)
                {
                    ExtractFieldsFromGeneratedParent(compilation, emitNullChecks, concatenatedFields, baseType, constructorSelector);
                }
                else
                {
                    ExtractFieldsFromConstructedParent(concatenatedFields, constructor);
                }
            }
        }
    }

    private static void ExtractFieldsFromConstructedParent(List<FieldInfo> concatenatedFields, IMethodSymbol constructor)
    {
        foreach (IParameterSymbol parameter in constructor.Parameters)
        {
            int index = concatenatedFields.FindIndex(p => p.ParameterName == parameter.Name);
            if (index != -1)
            {
                concatenatedFields[index].FieldType |= FieldType.PassedToBase;
            }
            else
            {
                concatenatedFields.Add(new FieldInfo(
                    parameter.Type,
                    parameter.Name,
                    string.Empty,
                    string.Empty,
                    parameter.Type,
                    IsNullable(parameter.Type),
                    null,
                    false,
                    FieldType.PassedToBase));
            }
        }
    }

    private static void ExtractFieldsFromGeneratedParent(Compilation compilation, bool emitNullChecks, List<FieldInfo> concatenatedFields, INamedTypeSymbol symbol, IConstructorSelector constructorSelector)
    {
        foreach (FieldInfo parameter in GetFieldsFromSymbol(compilation, symbol, emitNullChecks))
        {
            int index = concatenatedFields.FindIndex(p => p.ParameterName == parameter.ParameterName);
            if (index != -1)
            {
                concatenatedFields[index].FieldType |= FieldType.PassedToBase;
            }
            else
            {
                concatenatedFields.Add(new FieldInfo(
                    parameter.Type,
                    parameter.ParameterName,
                    string.Empty,
                    string.Empty,
                    parameter.FallbackType,
                    IsNullable(parameter.FallbackType),
                    null,
                    false,
                    FieldType.PassedToBase));
            }
        }

        ExtractFieldsFromParent(compilation, symbol, emitNullChecks, concatenatedFields, constructorSelector);
    }
}
