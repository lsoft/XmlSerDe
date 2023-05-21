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
            ITypeSymbol type,
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
                        new Builtin(compilation.DateTime(), "dateTime", false),
                        new Builtin(compilation.NDateTime(), "dateTime", false),

                        new Builtin(compilation.Guid(), "guid", false),
                        new Builtin(compilation.NGuid(), "guid", false),

                        new Builtin(compilation.Bool(), "boolean", false),
                        new Builtin(compilation.NBool(), "boolean", false),

                        new Builtin(compilation.SByte(), "byte", false),
                        new Builtin(compilation.NSByte(), "byte", false),

                        new Builtin(compilation.Byte(), "unsignedByte", false),
                        new Builtin(compilation.NByte(), "unsignedByte", false),

                        new Builtin(compilation.UInt16(), "unsignedShort", false),
                        new Builtin(compilation.NUInt16(), "unsignedShort", false),

                        new Builtin(compilation.Int16(), "short", false),
                        new Builtin(compilation.NInt16(), "short", false),

                        new Builtin(compilation.UInt32(), "unsignedInt", false),
                        new Builtin(compilation.NUInt32(), "unsignedInt", false),

                        new Builtin(compilation.Int32(), "int", false),
                        new Builtin(compilation.NInt32(), "int", false),

                        new Builtin(compilation.UInt64(), "unsignedLong", false),
                        new Builtin(compilation.NUInt64(), "unsignedLong", false),

                        new Builtin(compilation.Int64(), "long", false),
                        new Builtin(compilation.NInt64(), "long", false),

                        new Builtin(compilation.Decimal(), "decimal", false),
                        new Builtin(compilation.NDecimal(), "decimal", false),

                        new Builtin(compilation.String(), "string", true),
                        //TODO other builtin branches (+IExhauster, +IInjector)
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
            INamedTypeSymbol injectorType
            )
        {
            var sb = new StringBuilder();

            var injectorTypeGlobalName = injectorType.ToGlobalDisplayString();
            var injectorTypeAliasName = "inj" + injectorTypeGlobalName.GetHashCode().ToString().Replace("-", "_");

            sb.AppendLine($@"
namespace {typeof(BuiltinSourceProducer).Namespace}");

            sb.AppendLine($$"""
{
    using System;
    using roschar = System.ReadOnlySpan<char>;
    using {{injectorTypeAliasName}} = {{injectorTypeGlobalName}};
    using MethodImpl = global::System.Runtime.CompilerServices.MethodImplAttribute;
    using MethodImplOptions = global::System.Runtime.CompilerServices.MethodImplOptions;

    public static partial class {{BuiltinCodeHelperClassName}}
    {

""");

            GenerateDeserializeMethods(sb, injectorTypeAliasName);

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
        public static void {{AppendXmlHeadMethodName}}({{exhaustTypeAliasName}} exh)
        {
            exh.{{nameof(IExhauster.Append)}}(XmlHead);
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

            var headless = trimmedXml.Slice(index + endOfHead.Length).Trim();
            var lcl = XmlSerDe.Common.{{nameof(XmlNode2)}}.{{nameof(XmlNode2.GetLeadingCommentLengthIfExists)}}(headless);
            if(lcl < 0)
            {
                return headless;
            }

            var headless2 = headless.Slice(lcl);
            return headless2;
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

                var xmlTypeNameOpenVar = $"XmlTypeName{bi}";
                var xmlTypeNameCloseVar = $"XmlTypeNameClose{bi}";

                var methodName = (builtin.IsNeedToGuardWhenEncoded ? nameof(IExhauster.AppendEncoded) : nameof(IExhauster.Append));

                sb.AppendLine($$"""
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void {{ClassSourceProducer.HeadSerializeMethodName}}({{exhaustTypeAliasName}} exh, {{builtin.Symbol.ToGlobalDisplayString()}} obj)
        {
            exh.Append({{xmlTypeNameOpenVar}});
            exh.{{methodName}}(obj);
            exh.Append({{xmlTypeNameCloseVar}});
        }
""");

                sb.AppendLine($$"""
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void {{ClassSourceProducer.HeadlessSerializeMethodName}}({{exhaustTypeAliasName}} exh, {{builtin.Symbol.ToGlobalDisplayString()}} obj)
        {
            exh.{{methodName}}(obj);
        }
""");
            }

        }

        private void GenerateDeserializeMethods(
            StringBuilder sb,
            string injectorTypeAliasName
            )
        {
            if (sb is null)
            {
                throw new ArgumentNullException(nameof(sb));
            }

            foreach (var builtin in Builtins.Builtins)
            {
                sb.AppendLine($$"""
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void {{ClassSourceProducer.HeadDeserializeMethodName}}({{injectorTypeAliasName}} inj, roschar fullNode, roschar xmlnsAttributeName, out {{builtin.Symbol.ToGlobalDisplayString()}} result)
        {
            inj.{{nameof(IInjector.Parse)}}(fullNode, xmlnsAttributeName, out result);
        }
""");
            }

        }
    }
}
