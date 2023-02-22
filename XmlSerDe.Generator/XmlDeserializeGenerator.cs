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
using XmlSerDe.Common;
using System.IO;

namespace XmlSerDe.Generator
{
    [Generator(LanguageNames.CSharp)]
    public class XmlDeserializeGenerator : IIncrementalGenerator
    {
        public static readonly string SubjectAttributeFullName = typeof(XmlSubjectAttribute).FullName;
        public static readonly string DerivedSubjectAttributeFullName = typeof(XmlDerivedSubjectAttribute).FullName;
        public static readonly string FactoryAttributeFullName = typeof(XmlFactoryAttribute).FullName;
        public static readonly string ExhausterAttributeFullName = typeof(XmlExhausterAttribute).FullName;
        public static readonly string ParserAttributeFullName = typeof(XmlParserAttribute).FullName;

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
                var adder = new DocumentAdder(context);

                var bsg = new BuiltinSourceProducer(
                    compilation
                    );
                var bsgMainPart = bsg.GenerateMainPart(
                    );
                adder.AddDocumentToCompilation(
                    $"XmlSerDe.{BuiltinSourceProducer.BuiltinCodeHelperClassName}.MainPart.g.cs",
                    SourceText.From(bsgMainPart, Encoding.UTF8)
                    );
                var bsgDeserializationBody = bsg.GenerateDeserializationBody(
                    );
                adder.AddDocumentToCompilation(
                    $"XmlSerDe.{BuiltinSourceProducer.BuiltinCodeHelperClassName}.Deserialization.g.cs",
                    SourceText.From(bsgDeserializationBody, Encoding.UTF8)
                    );
                var bsgSerializationSharedBody = bsg.GenerateSerializationSharedBody(
                    );
                adder.AddDocumentToCompilation(
                    $"XmlSerDe.{BuiltinSourceProducer.BuiltinCodeHelperClassName}.Serialization.Shared.g.cs",
                    SourceText.From(bsgSerializationSharedBody, Encoding.UTF8)
                    );

                GenerateSources(adder, compilation, classesSyntax);
            }
            catch (Exception excp)
            {
                var msg = (excp.Message + Environment.NewLine + excp.StackTrace)
                    .Replace('\r', ' ')
                    .Replace('\n', ' ');

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

        private static void GenerateSources(
            DocumentAdder adder,
            Compilation compilation,
            ImmutableArray<ClassDeclarationSyntax> classesSyntax
            )
        {
            if (classesSyntax.IsDefaultOrEmpty)
            {
                // nothing to do yet
                return;
            }

            INamedTypeSymbol? markerAttribute = compilation.GetTypeByMetadataName(SubjectAttributeFullName);
            if (markerAttribute == null)
            {
                // If this is null, the compilation couldn't find the marker attribute type
                // which suggests there's something very wrong! Bail out..
                return;
            }

            // I'm not sure if this is actually necessary, but `[LoggerMessage]` does it, so seems like a good idea!
            IEnumerable<ClassDeclarationSyntax> distinctClasses = classesSyntax.Distinct();

            // Convert each ClassDeclarationSyntax to an ClassesToGenerate
            var classesToGenerate = GetClassesToGenerate(compilation, distinctClasses, adder.Context.CancellationToken);

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

                    var sp = new ClassSourceProducer(
                        compilation,
                        classToGenerate.Symbol
                        );

                    //generate builtin source
                    var bsg = new BuiltinSourceProducer(
                        compilation
                        );
                    foreach (var exhaustType in sp.SerializationInfoCollection.ExhaustList)
                    {
                        var bsgBody = bsg.GenerateSerializationBody(
                            exhaustType
                            );
                        adder.AddDocumentToCompilation(
                            $"XmlSerDe.{BuiltinSourceProducer.BuiltinCodeHelperClassName}.{exhaustType.Name}.g.cs",
                            SourceText.From(bsgBody, Encoding.UTF8)
                            );
                    }

                    var source = sp.GenerateClass();

                    adder.AddDocumentToCompilation(
                        $"{classToGenerate.Symbol.Name}.g.cs",
                        SourceText.From(source, Encoding.UTF8)
                        );

                }
            }
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

        private readonly struct DocumentAdder
        {
            public readonly SourceProductionContext Context;

            private readonly HashSet<string> _filePaths = new HashSet<string>();

            public DocumentAdder(
                SourceProductionContext context
                )
            {
                Context = context;
            }

            public void AddDocumentToCompilation(
                string documentName,
                SourceText document
                )
            {
                if (string.IsNullOrEmpty(documentName))
                {
                    throw new ArgumentException($"'{nameof(documentName)}' cannot be null or empty.", nameof(documentName));
                }

                if (document is null)
                {
                    throw new ArgumentNullException(nameof(document));
                }

                if (!_filePaths.Add(documentName))
                {
                    return;
                }

                Context.AddSource(
                    documentName,
                    document
                    );
            }
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
    }
}
#endif
