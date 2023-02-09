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
        public const string AppendXmlHeadMethodName = "AppendXmlHead";
        public const string WriteStringToStreamMethodName = "WriteStringToStream";
        public const string WriteEncodedStringToStreamMethodName = "WriteEncodedStringToStream";

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
                        new Builtin("global::System.DateTime.Parse({0})", compilation.DateTime(), "dateTime", "{0}.ToString()", false),
                        new Builtin("global::System.DateTime.Parse({0})", compilation.NDateTime(), "dateTime", "{0}.ToString()", false),

                        new Builtin("global::System.Guid.Parse({0})", compilation.Guid(), "guid", "{0}.ToString()", false),
                        new Builtin("global::System.Guid.Parse({0})", compilation.NGuid(), "guid", "{0}.ToString()", false),

                        new Builtin("global::System.Boolean.Parse({0})", compilation.Bool(), "boolean", @"({0} ? ""True"" : ""False"")", false),
                        new Builtin("global::System.Net.WebUtility.HtmlDecode({0}.ToString())", compilation.String(), "string", "{0}", true),

                        new Builtin("global::System.SByte.Parse({0}, provider: global::System.Globalization.CultureInfo.InvariantCulture)", compilation.SByte(), "byte", "{0}.ToString()", false),
                        new Builtin("global::System.Byte.Parse({0}, provider: global::System.Globalization.CultureInfo.InvariantCulture)", compilation.Byte(), "unsignedByte", "{0}.ToString()", false),

                        new Builtin("global::System.UInt16.Parse({0}, provider: global::System.Globalization.CultureInfo.InvariantCulture)", compilation.UInt16(), "unsignedShort", "{0}.ToString()", false),
                        new Builtin("global::System.Int16.Parse({0}, provider: global::System.Globalization.CultureInfo.InvariantCulture)", compilation.Int16(), "short", "{0}.ToString()", false),

                        new Builtin("global::System.UInt32.Parse({0}, provider: global::System.Globalization.CultureInfo.InvariantCulture)", compilation.UInt32(), "unsignedInt", "{0}.ToString()", false),
                        new Builtin("global::System.Int32.Parse({0}, provider: global::System.Globalization.CultureInfo.InvariantCulture)", compilation.Int32(), "int", "{0}.ToString()", false),
                        new Builtin("global::System.Int32.Parse({0}, provider: global::System.Globalization.CultureInfo.InvariantCulture)", compilation.NInt32(), "int", "{0}.ToString()", false),

                        new Builtin("global::System.UInt64.Parse({0}, provider: global::System.Globalization.CultureInfo.InvariantCulture)", compilation.UInt64(), "unsignedLong", "{0}.ToString()", false),
                        new Builtin("global::System.Int64.Parse({0}, provider: global::System.Globalization.CultureInfo.InvariantCulture)", compilation.Int64(), "long", "{0}.ToString()", false),
                        new Builtin("global::System.Int64.Parse({0}, provider: global::System.Globalization.CultureInfo.InvariantCulture)", compilation.NInt64(), "long", "{0}.ToString()", false),

                        new Builtin("global::System.Decimal.Parse({0}, provider: global::System.Globalization.CultureInfo.InvariantCulture)", compilation.Decimal(), "decimal", "{0}.ToString()", false),
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
        private static readonly byte[] XmlHeadBytes = global::System.Text.Encoding.UTF8.GetBytes("<?xml version=\"1.0\" encoding=\"utf-8\"?>");

""");

            GenerateWriteStringToStream(sb);
            GenerateWriteEncodedStringToStream(sb);
            GenerateAppendXmlHeadMethod(sb);
            GenerateCutXmlHeadMethod(sb);

            //GenerateSerializeMethods(sb);
            GenerateDeserializeMethods(sb);

            sb.AppendLine($$"""
    }
}
""");

            return sb.ToString();
        }

        private void GenerateWriteStringToStream(StringBuilder sb)
        {
            sb.AppendLine($$"""
        //stackalloc here: do not inline this method!
        public static void {{WriteStringToStreamMethodName}}(global::System.IO.Stream stream, string inputString)
        {
            var byteCount = global::System.Text.Encoding.UTF8.GetByteCount(inputString);
            var buffer = byteCount < 256 ? stackalloc byte[byteCount] : new byte[byteCount];
            global::System.Text.Encoding.UTF8.GetBytes(inputString.AsSpan(), buffer);
            stream.Write(buffer);
        }

""");
        }

        private void GenerateWriteEncodedStringToStream(StringBuilder sb)
        {
            sb.AppendLine($$"""
        //stackalloc here: do not inline this method!
        public static void {{WriteEncodedStringToStreamMethodName}}(global::System.IO.Stream stream, string inputString)
        {
            var encodedString = global::System.Net.WebUtility.HtmlEncode(inputString);
            var byteCount = global::System.Text.Encoding.UTF8.GetByteCount(encodedString);
            var buffer = byteCount < 256 ? stackalloc byte[byteCount] : new byte[byteCount];
            global::System.Text.Encoding.UTF8.GetBytes(encodedString.AsSpan(), buffer);
            stream.Write(buffer);
        }

""");
        }

        private void GenerateAppendXmlHeadMethod(StringBuilder sb)
        {
            sb.AppendLine($$"""
        public static void {{AppendXmlHeadMethodName}}(global::System.IO.Stream stream)
        {
            stream.Write(XmlHeadBytes, 0, XmlHeadBytes.Length);
        }

""");
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

//        private void GenerateSerializeMethods(StringBuilder sb)
//        {
//            foreach (var builtin in Builtins.Builtins)
//            {
//                var toStringClause = string.Format(
//                    builtin.ToStringClause,
//                    "obj"
//                    );

//                var methodName = builtin.IsNeedToGuardWhenEncoded
//                    ? WriteEncodedStringToStreamMethodName
//                    : WriteStringToStreamMethodName
//                    ;

//                sb.AppendLine($$"""
//        public static void {{ClassSourceProducer.HeadSerializeMethodName}}(global::System.IO.Stream stream, {{builtin.Symbol.ToGlobalDisplayString()}} obj)
//        {
//            var writeValue = {{toStringClause}};
//            {{methodName}}(stream, writeValue);
//        }
//""");
//            }

//        }

        private void GenerateDeserializeMethods(StringBuilder sb)
        {
            foreach (var builtin in Builtins.Builtins)
            {
                var parseClause = string.Format(
                    builtin.ConverterClause,
                    $"xmlNode.{nameof(XmlNode2.Internals)}"
                    );


                sb.AppendLine($$"""
        public static void {{ClassSourceProducer.HeadDeserializeMethodName}}(roschar fullNode, roschar xmlnsAttributeName, out {{builtin.Symbol.ToGlobalDisplayString()}} result)
        {
            var xmlNode = new {{typeof(XmlNode2).FullName}}(fullNode, xmlnsAttributeName);
            var xmlNodeDeclaredType = xmlNode.{{nameof(XmlNode2.DeclaredNodeType)}};

            if(!xmlNodeDeclaredType.SequenceEqual("{{builtin.XmlTypeName}}".AsSpan()))
            {
                throw new InvalidOperationException("(0) [C# node {{builtin.Symbol.Name}}] Unknown type " + xmlNodeDeclaredType.ToString());
            }

            result = {{parseClause}};
        }
""");
            }

        }
    }
}
