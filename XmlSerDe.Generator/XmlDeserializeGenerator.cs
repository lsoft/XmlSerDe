#if NETSTANDARD
using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using XmlSerDe.Generator.Producer;
using XmlSerDe.Generator.Helper;
using XmlSerDe.Generator;
using XmlSerDe.Generator.EmbeddedCode;

namespace XmlSerDe.Generator
{
    [Generator(LanguageNames.CSharp)]
    public class XmlDeserializeGenerator : IIncrementalGenerator
    {
        public static readonly string RootAttributeFullName = typeof(XmlRootAttribute).FullName;
        public static readonly string SubjectAttributeFullName = typeof(XmlSubjectAttribute).FullName;
        public static readonly string DerivedSubjectAttributeFullName = typeof(XmlDerivedSubjectAttribute).FullName;

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // Do a simple filter for classes
            IncrementalValuesProvider<ClassDeclarationSyntax> classDeclarations = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (s, _) => IsSyntaxTargetForGeneration(s), // select classes with attributes
                    transform: static (ctx, _) => GetSemanticTargetForGeneration(ctx)) // select the class with the [SeparateThreadWrapper] attribute
                .Where(static m => m is not null)!; // filter out attributed classes that we don't care about

            // Combine the selected classes with the `Compilation`
            IncrementalValueProvider<(Compilation, ImmutableArray<ClassDeclarationSyntax>)> compilationAndClasses
                = context.CompilationProvider.Combine(classDeclarations.Collect());

            // Generate the source using the compilation and classes
            context.RegisterSourceOutput(
                compilationAndClasses,
                static (spc, source) => Execute(spc, source.Item1, source.Item2)
                );
        }

        private static void Execute(SourceProductionContext context, Compilation compilation, ImmutableArray<ClassDeclarationSyntax> classesSyntax)
        {
            try
            {
                context.AddSource(
                    "XmlSerDeHelper.cs",
                    GeneratorResources.EmbeddedHelperCode
                    );

                var generated = InternalExecute(context, compilation, classesSyntax);
                foreach (var gen in generated)
                {
                    context.AddSource(gen.FileName, gen.Source);
                }
            }
            catch (Exception excp)
            {
                var msg = excp.Message;

                context.ReportDiagnostic(
                    Diagnostic.Create(
                        new DiagnosticDescriptor(
                            id: "XmlSerDe",
                            title: msg,
                            messageFormat: msg,
                            category: "XmlSerDe",
                            DiagnosticSeverity.Error,
                            isEnabledByDefault: true
                            ),
                        Location.None
                        )
                    );
            }
        }

        private static List<GeneratedSource> InternalExecute(SourceProductionContext context, Compilation compilation, ImmutableArray<ClassDeclarationSyntax> classesSyntax)
        {
            var result = new List<GeneratedSource>();

            if (classesSyntax.IsDefaultOrEmpty)
            {
                // nothing to do yet
                return result;
            }

            INamedTypeSymbol? markerAttribute = compilation.GetTypeByMetadataName(RootAttributeFullName);
            if (markerAttribute == null)
            {
                // If this is null, the compilation couldn't find the marker attribute type
                // which suggests there's something very wrong! Bail out..
                return result;
            }

            // I'm not sure if this is actually necessary, but `[LoggerMessage]` does it, so seems like a good idea!
            IEnumerable<ClassDeclarationSyntax> distinctClasses = classesSyntax.Distinct();

            // Convert each ClassDeclarationSyntax to an ClassesToGenerate
            var classesToGenerate = GetClassesToGenerate(compilation, distinctClasses, context.CancellationToken);

            // If there were errors in the ClassDeclarationSyntax, we won't create an
            // ClassesToGenerate for it, so make sure we have something to generate
            if (classesToGenerate.Count > 0)
            {
                foreach (var classToGenerate in classesToGenerate)
                {
                    var ctgs = classToGenerate.Symbol;

                    if (!ctgs.IsPartial())
                    {
                        throw new InvalidOperationException($"Class {ctgs.ToFullDisplayString()} should be partial");
                    }
                    //if (ctgs.Constructors.All(c => c.Parameters.Length > 0))
                    //{
                    //    throw new InvalidOperationException($"Class {ctgs.ToFullDisplayString()} should have parameterless constructor");
                    //}

                    var sp = new DeserializeSourceProducer(
                        compilation,
                        classToGenerate.Symbol
                        );

                    var source = sp.GenerateDeserializer();

                    result.Add(
                        new GeneratedSource(
                            $"{classToGenerate.Symbol.Name}.g.cs",
                            SourceText.From(source, Encoding.UTF8)
                            )
                        );
                }
            }

            return result;
        }

        private static List<ClassToGenerate> GetClassesToGenerate(
            Compilation compilation,
            IEnumerable<ClassDeclarationSyntax> cdss,
            CancellationToken ct
            )
        {
            // Create a list to hold our output
            var result = new List<ClassToGenerate>();

            foreach (var cds in cdss)
            {
                // stop if we're asked to
                ct.ThrowIfCancellationRequested();

                // Get the semantic representation of the class syntax
                SemanticModel semanticModel = compilation.GetSemanticModel(cds.SyntaxTree);
                if (semanticModel.GetDeclaredSymbol(cds) is not INamedTypeSymbol classSymbol)
                {
                    // something went wrong, bail out
                    continue;
                }

                // Create an TypeToGenerate for use in the generation phase
                result.Add(new ClassToGenerate(semanticModel, classSymbol));
            }

            return result;
        }

        private static bool IsSyntaxTargetForGeneration(SyntaxNode node)
            => node is ClassDeclarationSyntax cds && cds.AttributeLists.Count > 0;

        private static ClassDeclarationSyntax? GetSemanticTargetForGeneration(GeneratorSyntaxContext context)
        {
            // we know the node is a ClassDeclarationSyntax thanks to IsSyntaxTargetForGeneration
            var cds = (ClassDeclarationSyntax)context.Node;

            if(!(context.SemanticModel.GetDeclaredSymbol(cds) is INamedTypeSymbol nts))
            {
                return null;
            }

            foreach(var attributeSymbol in nts.GetAttributes())
            {
                if(attributeSymbol.AttributeClass?.ToFullDisplayString().In(
                    RootAttributeFullName,
                    SubjectAttributeFullName,
                    DerivedSubjectAttributeFullName
                    ) ?? false)
                {
                    return cds;
                }
            }

            // we didn't find the attribute we were looking for
            return null;
        }

        private readonly struct ClassToGenerate
        {
            public readonly SemanticModel SemanticModel;
            public readonly INamedTypeSymbol Symbol;

            public ClassToGenerate(SemanticModel semanticModel, INamedTypeSymbol symbol)
            {
                SemanticModel = semanticModel;
                Symbol = symbol;
            }
        }

        private readonly struct GeneratedSource
        {
            public readonly string FileName;
            public readonly SourceText Source;

            public GeneratedSource(string fileName, SourceText source)
            {
                FileName = fileName;
                Source = source;
            }
        }
    }
}
#endif
