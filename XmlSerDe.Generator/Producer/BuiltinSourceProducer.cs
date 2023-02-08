using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using XmlSerDe.Generator.EmbeddedCode;
using XmlSerDe.Generator.Helper;

namespace XmlSerDe.Generator.Producer
{
    internal readonly struct BuiltinSourceProducer
    {
        public const string BuiltinCodeParserClassName = "BuiltinCodeParser";
        public const string CutXmlHeadMethodName = "CutXmlHead";

        private readonly Compilation _compilation;
        public readonly BuiltinCollection Builtins;

        public BuiltinSourceProducer(
            Compilation compilation
            )
        {
            if (compilation is null)
            {
                throw new ArgumentNullException(nameof(compilation));
            }

            _compilation = compilation;

            Builtins = GenerateBuiltins(compilation);
        }

        public static bool TryGetBuiltin(
            Compilation compilation,
            INamedTypeSymbol type,
            out Builtin builtin)
        {
            var builtins = GenerateBuiltins(compilation);
            return builtins.TryGet(type, out builtin);
        }

        public static BuiltinCollection GenerateBuiltins(Compilation compilation)
        {
            return
                new BuiltinCollection(
                    new List<Builtin>
                    {
                        new Builtin("global::System.DateTime.Parse({0})", compilation.DateTime(), "dateTime"),
                        new Builtin("global::System.DateTime.Parse({0})", compilation.NDateTime(), "dateTime"),

                        new Builtin("global::System.Guid.Parse({0})", compilation.Guid(), "guid"),
                        new Builtin("global::System.Guid.Parse({0})", compilation.NGuid(), "guid"),

                        new Builtin("global::System.Boolean.Parse({0})", compilation.Bool(), "boolean"),
                        new Builtin("global::System.Net.WebUtility.HtmlDecode({0}.ToString())", compilation.String(), "string"),

                        new Builtin("global::System.SByte.Parse({0}, provider: global::System.Globalization.CultureInfo.InvariantCulture)", compilation.SByte(), "byte"),
                        new Builtin("global::System.Byte.Parse({0}, provider: global::System.Globalization.CultureInfo.InvariantCulture)", compilation.Byte(), "unsignedByte"),

                        new Builtin("global::System.UInt16.Parse({0}, provider: global::System.Globalization.CultureInfo.InvariantCulture)", compilation.UInt16(), "unsignedShort"),
                        new Builtin("global::System.Int16.Parse({0}, provider: global::System.Globalization.CultureInfo.InvariantCulture)", compilation.Int16(), "short"),

                        new Builtin("global::System.UInt32.Parse({0}, provider: global::System.Globalization.CultureInfo.InvariantCulture)", compilation.UInt32(), "unsignedInt"),
                        new Builtin("global::System.Int32.Parse({0}, provider: global::System.Globalization.CultureInfo.InvariantCulture)", compilation.Int32(), "int"),
                        new Builtin("global::System.Int32.Parse({0}, provider: global::System.Globalization.CultureInfo.InvariantCulture)", compilation.NInt32(), "int"),

                        new Builtin("global::System.UInt64.Parse({0}, provider: global::System.Globalization.CultureInfo.InvariantCulture)", compilation.UInt64(), "unsignedLong"),
                        new Builtin("global::System.Int64.Parse({0}, provider: global::System.Globalization.CultureInfo.InvariantCulture)", compilation.Int64(), "long"),
                        new Builtin("global::System.Int64.Parse({0}, provider: global::System.Globalization.CultureInfo.InvariantCulture)", compilation.NInt64(), "long"),

                        new Builtin("global::System.Decimal.Parse({0}, provider: global::System.Globalization.CultureInfo.InvariantCulture)", compilation.Decimal(), "decimal"),
                        //TODO other builtin branches
                    }
                );
        }

        public string GenerateAllMethods()
        {
            var sb = new StringBuilder();

            sb.AppendLine($@"
namespace {typeof(BuiltinSourceProducer).Namespace}");

            sb.AppendLine($$"""
{
    using System;
    using roschar = System.ReadOnlySpan<char>;

    public static class {{BuiltinCodeParserClassName}}
    {

""");

            GenerateCutXmlHeadMethod(sb);
            GenerateDeserializeMethods(sb);

            sb.AppendLine($$"""
    }
}
""");

            return sb.ToString();
        }

        private void GenerateCutXmlHeadMethod(StringBuilder sb)
        {
            sb.AppendLine($$"""
        public static roschar {{CutXmlHeadMethodName}}(roschar xml)
        {
            var startOfHead = "<?xml".AsSpan();

            var trimmedXml = xml.Trim();
            if (!trimmedXml.StartsWith(startOfHead))
            {
                return trimmedXml;
            }

            var endOfHead = "?>".AsSpan();
            var index = trimmedXml.IndexOf(endOfHead);

            return trimmedXml.Slice(index + endOfHead.Length).Trim();
        }

""");
        }

        private void GenerateDeserializeMethods(StringBuilder sb)
        {
            foreach (var builtin in Builtins.Builtins)
            {
                var finalClause = string.Format(
                    builtin.ConverterClause,
                    $"xmlNode.{nameof(XmlNode2.Internals)}"
                    );


                sb.AppendLine($$"""
        public static void {{DeserializeSourceProducer.HeadDeserializeMethodName}}(roschar fullNode, roschar xmlnsAttributeName, out {{builtin.Symbol.ToGlobalDisplayString()}} result)
        {
            var xmlNode = new {{typeof(XmlNode2).FullName}}(fullNode, xmlnsAttributeName);
            var xmlNodeDeclaredType = xmlNode.{{nameof(XmlNode2.DeclaredNodeType)}};

            if(!xmlNodeDeclaredType.SequenceEqual("{{builtin.XmlTypeName}}".AsSpan()))
            {
                throw new InvalidOperationException("(0) [C# node {{builtin.Symbol.Name}}] Unknown type " + xmlNodeDeclaredType.ToString());
            }

            result = {{finalClause}};
        }
""");
            }

        }
    }
}
