using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XmlSerDe.Tests
{
    //public static class TestHelper
    //{
    //    public static string Verify(string source)
    //    {
    //        // Parse the provided string into a C# syntax tree
    //        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source);

    //        IEnumerable<PortableExecutableReference> references = new[]
    //        {
    //            MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
    //        };

    //        // Create a Roslyn compilation for the syntax tree.
    //        CSharpCompilation compilation = CSharpCompilation.Create(
    //            assemblyName: "Tests",
    //            syntaxTrees: new[] { syntaxTree },
    //            references: references
    //            );


    //        // Create an instance of our EnumGenerator incremental source generator
    //        var generator = new XmlSerDe.Generator.XmlDeserializeGenerator();

    //        // The GeneratorDriver is used to run our generator against a compilation
    //        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

    //        // Run the source generator!
    //        driver = driver.RunGeneratorsAndUpdateCompilation(
    //            compilation,
    //            out var outputCompilation,
    //            out var diagnostics);

    //        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
    //        string errorMsg = null;
    //        if (errors.Count != 0)
    //        {
    //            throw new InvalidOperationException(string.Join(Environment.NewLine, errors.Select(e => e.GetMessage())));
    //        }

    //        return outputCompilation.SyntaxTrees.Skip(1).LastOrDefault()?.ToString();
    //    }
    //}
}
