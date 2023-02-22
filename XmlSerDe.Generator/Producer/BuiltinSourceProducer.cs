using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using XmlSerDe.Common;
using XmlSerDe.Generator.Helper;

namespace XmlSerDe.Generator.Producer
{
    internal readonly struct BuiltinSourceProducer
    {
        public const string BuiltinCodeHelperClassName = "BuiltinCodeHelper";
        public const string CutXmlHeadMethodName = "CutXmlHead";
        public const string AppendXmlHeadMethodName = "AppendXmlHead";

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
                        new Builtin("global::System.DateTime.Parse({0})", compilation.DateTime(), "dateTime", "{0}", false),
                        new Builtin("global::System.DateTime.Parse({0})", compilation.NDateTime(), "dateTime", "{0}", false),

                        new Builtin("global::System.Guid.Parse({0})", compilation.Guid(), "guid", "{0}", false),
                        new Builtin("global::System.Guid.Parse({0})", compilation.NGuid(), "guid", "{0}", false),

                        new Builtin("global::System.Boolean.Parse({0})", compilation.Bool(), "boolean", @"{0}", false),
                        new Builtin("global::System.Boolean.Parse({0})", compilation.NBool(), "boolean", @"{0}", false),

                        new Builtin("global::System.Net.WebUtility.HtmlDecode({0}.ToString())", compilation.String(), "string", "{0}", true),

                        new Builtin("global::System.SByte.Parse({0}, provider: global::System.Globalization.CultureInfo.InvariantCulture)", compilation.SByte(), "byte", "{0}", false),
                        new Builtin("global::System.SByte.Parse({0}, provider: global::System.Globalization.CultureInfo.InvariantCulture)", compilation.NSByte(), "byte", "{0}", false),

                        new Builtin("global::System.Byte.Parse({0}, provider: global::System.Globalization.CultureInfo.InvariantCulture)", compilation.Byte(), "unsignedByte", "{0}", false),
                        new Builtin("global::System.Byte.Parse({0}, provider: global::System.Globalization.CultureInfo.InvariantCulture)", compilation.NByte(), "unsignedByte", "{0}", false),

                        new Builtin("global::System.UInt16.Parse({0}, provider: global::System.Globalization.CultureInfo.InvariantCulture)", compilation.UInt16(), "unsignedShort", "{0}", false),
                        new Builtin("global::System.UInt16.Parse({0}, provider: global::System.Globalization.CultureInfo.InvariantCulture)", compilation.NUInt16(), "unsignedShort", "{0}", false),

                        new Builtin("global::System.Int16.Parse({0}, provider: global::System.Globalization.CultureInfo.InvariantCulture)", compilation.Int16(), "short", "{0}", false),
                        new Builtin("global::System.Int16.Parse({0}, provider: global::System.Globalization.CultureInfo.InvariantCulture)", compilation.NInt16(), "short", "{0}", false),

                        new Builtin("global::System.UInt32.Parse({0}, provider: global::System.Globalization.CultureInfo.InvariantCulture)", compilation.UInt32(), "unsignedInt", "{0}", false),
                        new Builtin("global::System.UInt32.Parse({0}, provider: global::System.Globalization.CultureInfo.InvariantCulture)", compilation.NUInt32(), "unsignedInt", "{0}", false),

                        new Builtin("global::System.Int32.Parse({0}, provider: global::System.Globalization.CultureInfo.InvariantCulture)", compilation.Int32(), "int", "{0}", false),
                        new Builtin("global::System.Int32.Parse({0}, provider: global::System.Globalization.CultureInfo.InvariantCulture)", compilation.NInt32(), "int", "{0}", false),

                        new Builtin("global::System.UInt64.Parse({0}, provider: global::System.Globalization.CultureInfo.InvariantCulture)", compilation.UInt64(), "unsignedLong", "{0}", false),
                        new Builtin("global::System.UInt64.Parse({0}, provider: global::System.Globalization.CultureInfo.InvariantCulture)", compilation.NUInt64(), "unsignedLong", "{0}", false),

                        new Builtin("global::System.Int64.Parse({0}, provider: global::System.Globalization.CultureInfo.InvariantCulture)", compilation.Int64(), "long", "{0}", false),
                        new Builtin("global::System.Int64.Parse({0}, provider: global::System.Globalization.CultureInfo.InvariantCulture)", compilation.NInt64(), "long", "{0}", false),

                        new Builtin("global::System.Decimal.Parse({0}, provider: global::System.Globalization.CultureInfo.InvariantCulture)", compilation.Decimal(), "decimal", "{0}", false),
                        new Builtin("global::System.Decimal.Parse({0}, provider: global::System.Globalization.CultureInfo.InvariantCulture)", compilation.NDecimal(), "decimal", "{0}", false),
                        //TODO other builtin branches
                    }
                );
        }

        public string GenerateMainPart()
        {
            var sb = new StringBuilder();

            sb.AppendLine($@"
namespace {typeof(BuiltinSourceProducer).Namespace}");

            sb.AppendLine($$"""
{
    using System;
    using roschar = System.ReadOnlySpan<char>;

    public static partial class {{BuiltinCodeHelperClassName}}
    {
        private static readonly string XmlHead = "<?xml version=\"1.0\" encoding=\"utf-8\"?>";


""");

            GenerateCutXmlHeadMethod(sb);

            sb.AppendLine($$"""
    }
}
""");

            return sb.ToString();
        }

        public string GenerateDeserializationBody(
            )
        {
            var sb = new StringBuilder();

            sb.AppendLine($@"
namespace {typeof(BuiltinSourceProducer).Namespace}");

            sb.AppendLine($$"""
{
    using System;
    using roschar = System.ReadOnlySpan<char>;

    public static partial class {{BuiltinCodeHelperClassName}}
    {

""");

            GenerateDeserializeMethods(sb);

            sb.AppendLine($$"""
    }
}
""");

            return sb.ToString();
        }

        public string GenerateSerializationSharedBody(
            )
        {
            var sb = new StringBuilder();

            sb.AppendLine($@"
namespace {typeof(BuiltinSourceProducer).Namespace}");

            sb.AppendLine($$"""
{
    using System;
    using roschar = System.ReadOnlySpan<char>;

    public static partial class {{BuiltinCodeHelperClassName}}
    {

""");

            GenerateSerializeConstants(sb);

            sb.AppendLine($$"""
    }
}
""");

            return sb.ToString();
        }

        public string GenerateSerializationBody(
            INamedTypeSymbol exhaustType
            )
        {
            if (exhaustType is null)
            {
                throw new ArgumentNullException(nameof(exhaustType));
            }

            var sb = new StringBuilder();

            var exhaustTypeGlobalName = exhaustType.ToGlobalDisplayString();
            var exhaustTypeAliasName = "exh" + exhaustTypeGlobalName.GetHashCode().ToString().Replace("-", "_");

            sb.AppendLine($@"
namespace {typeof(BuiltinSourceProducer).Namespace}");

            sb.AppendLine($$"""
{
    using System;
    using roschar = System.ReadOnlySpan<char>;
    using {{exhaustTypeAliasName}} = {{exhaustTypeGlobalName}};
    using MethodImpl = global::System.Runtime.CompilerServices.MethodImplAttribute;
    using MethodImplOptions = global::System.Runtime.CompilerServices.MethodImplOptions;

    public static partial class {{BuiltinCodeHelperClassName}}
    {

""");

            GenerateAppendXmlHeadMethod(sb, exhaustTypeAliasName);
            GenerateSerializeMethods(sb, exhaustTypeAliasName);

            sb.AppendLine($$"""
    }
}
""");

            return sb.ToString();
        }

        private void GenerateAppendXmlHeadMethod(
            StringBuilder sb,
            string exhaustTypeAliasName
            )
        {
            sb.AppendLine($$"""
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void {{AppendXmlHeadMethodName}}({{exhaustTypeAliasName}} sb)
        {
            sb.{{nameof(IExhauster.Append)}}(XmlHead);
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

        private void GenerateSerializeConstants(
            StringBuilder sb
            )
        {
            if (sb is null)
            {
                throw new ArgumentNullException(nameof(sb));
            }

            for (var bi = 0; bi < Builtins.Builtins.Count; bi++)
            {
                var builtin = Builtins.Builtins[bi];

                var xmlTypeNameOpenVar = $"XmlTypeName{bi}";
                var xmlTypeNameCloseVar = $"XmlTypeNameClose{bi}";

                sb.AppendLine($$"""
        private const string {{xmlTypeNameOpenVar}} = "<{{builtin.XmlTypeName}}>";
        private const string {{xmlTypeNameCloseVar}} = "</{{builtin.XmlTypeName}}>";
""");
            }
        }

        private void GenerateSerializeMethods(
            StringBuilder sb,
            string exhaustTypeAliasName
            )
        {
            if (sb is null)
            {
                throw new ArgumentNullException(nameof(sb));
            }

            if (exhaustTypeAliasName is null)
            {
                throw new ArgumentNullException(nameof(exhaustTypeAliasName));
            }

            for (var bi = 0; bi < Builtins.Builtins.Count; bi++)
            {
                var builtin = Builtins.Builtins[bi];

                var prepareBeforeWriteClause = string.Format(
                    builtin.PrepareBeforeWriteClause,
                    "obj"
                    );

                var buildWriteValue = builtin.IsNeedToGuardWhenEncoded
                    ? $"global::System.Net.WebUtility.HtmlEncode({prepareBeforeWriteClause})"
                    : prepareBeforeWriteClause;

                var xmlTypeNameOpenVar = $"XmlTypeName{bi}";
                var xmlTypeNameCloseVar = $"XmlTypeNameClose{bi}";

                sb.AppendLine($$"""
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void {{ClassSourceProducer.HeadSerializeMethodName}}({{exhaustTypeAliasName}} sb, {{builtin.Symbol.ToGlobalDisplayString()}} obj)
        {
            sb.Append({{xmlTypeNameOpenVar}});
            sb.Append({{buildWriteValue}});
            sb.Append({{xmlTypeNameCloseVar}});
        }
""");

                sb.AppendLine($$"""
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void {{ClassSourceProducer.HeadlessSerializeMethodName}}({{exhaustTypeAliasName}} sb, {{builtin.Symbol.ToGlobalDisplayString()}} obj)
        {
            sb.Append({{buildWriteValue}});
        }
""");
            }

        }

        private void GenerateDeserializeMethods(
            StringBuilder sb
            )
        {
            if (sb is null)
            {
                throw new ArgumentNullException(nameof(sb));
            }

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
